using System.Data;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.CancellationPolicies;
using Dapper;

namespace Bookify.Application.Bookings.CancelBooking;

internal sealed class CancelBookingCommandHandler : ICommandHandler<CancelBookingCommand>
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICancellationPolicyRepository _cancellationPolicyRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IJobScheduler _jobScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;

    public CancelBookingCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IBookingRepository bookingRepository,
        ICancellationPolicyRepository cancellationPolicyRepository,
        IApartmentRepository apartmentRepository,
        ISqlConnectionFactory sqlConnectionFactory,
        IJobScheduler jobScheduler,
        IUnitOfWork unitOfWork,
        IUserContext userContext)
    {
        _dateTimeProvider = dateTimeProvider;
        _bookingRepository = bookingRepository;
        _cancellationPolicyRepository = cancellationPolicyRepository;
        _apartmentRepository = apartmentRepository;
        _sqlConnectionFactory = sqlConnectionFactory;
        _jobScheduler = jobScheduler;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
    }

    public async Task<Result> Handle(
        CancelBookingCommand request,
        CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        Apartment? apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId, cancellationToken);
        if (apartment is null)
        {
            return Result.Failure(ApartmentErrors.NotFound);
        }

        Guid currentUserId = _userContext.UserId;
        bool cancelledByHost;
        if (currentUserId == booking.UserId)
        {
            cancelledByHost = false;
        }
        else if (currentUserId == apartment.OwnerId)
        {
            cancelledByHost = true;
        }
        else
        {
            return Result.Failure(BookingErrors.Unauthorized);
        }

        BookingReason? reason = null;
        if (request.ReasonType.HasValue && request.ReasonType.Value != ReasonType.None)
        {
            reason = BookingReason.Create(request.ReasonType.Value, request.ReasonDescription,
                _dateTimeProvider.UtcNow);
        }

        DateTime utcNow = _dateTimeProvider.UtcNow;
        Result<PenaltyResult> cancelResult;

        // 1. If PaymentStatus is Unpaid (booking was in PendingPayment and hasn't been paid yet)
        if (booking.PaymentStatus == PaymentStatus.Unpaid)
        {
            // Cancel TTL1 (Stripe Checkout Session Expiration Job)
            await _jobScheduler.CancelExpireCheckoutSessionAsync(booking.Id, cancellationToken);

            cancelResult = booking.Cancel(utcNow, null, null, cancelledByHost, reason);
            if (cancelResult.IsFailure)
            {
                return cancelResult;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // 2. If PaymentStatus is Authorized (booking was reserved but not yet captured/confirmed)
        if (booking.PaymentStatus == PaymentStatus.Authorized)
        {
            cancelResult = booking.Cancel(utcNow, null, null, cancelledByHost, reason);
            if (cancelResult.IsFailure)
            {
                return cancelResult;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // 3. Booking is paid (Confirmed) - apply cancellation policy and record penalties
        CancellationPolicy? policy = apartment.CancellationPolicyId.HasValue
            ? await _cancellationPolicyRepository.GetByIdAsync(apartment.CancellationPolicyId.Value, cancellationToken)
            : await _cancellationPolicyRepository.GetDefaultAsync(cancellationToken);
            
        if (policy is null)
        {
            return Result.Failure(BookingErrors.NoPolicyAvailable);
        }

        var engine = new CancellationPolicyEngine();
        cancelResult = booking.Cancel(utcNow, policy, engine, cancelledByHost, reason);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        PenaltyResult penaltyResult = cancelResult.Value;

        // Record host balances via direct insert with Dapper
        if (penaltyResult.GuestPenaltyAmount > 0 || penaltyResult.HostPenaltyAmount > 0)
        {
            using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

            if (penaltyResult.GuestPenaltyAmount > 0)
            {
                const string insertSql = """
                                         INSERT INTO host_balances (id, host_id, booking_id, amount, currency, reason, created_on_utc)
                                         VALUES (@Id, @HostId, @BookingId, @Amount, @Currency, @Reason, @CreatedOnUtc)
                                         """;

                await connection.ExecuteAsync(insertSql, new
                {
                    Id = Guid.CreateVersion7(),
                    HostId = apartment.OwnerId,
                    BookingId = booking.Id,
                    Amount = penaltyResult.GuestPenaltyAmount,
                    penaltyResult.Currency,
                    Reason = "guest_penalty",
                    CreatedOnUtc = utcNow
                });
            }

            if (penaltyResult.HostPenaltyAmount > 0)
            {
                const string insertSql = """
                                         INSERT INTO host_balances (id, host_id, booking_id, amount, currency, reason, created_on_utc)
                                         VALUES (@Id, @HostId, @BookingId, @Amount, @Currency, @Reason, @CreatedOnUtc)
                                         """;

                await connection.ExecuteAsync(insertSql, new
                {
                    Id = Guid.CreateVersion7(),
                    HostId = apartment.OwnerId,
                    BookingId = booking.Id,
                    Amount = penaltyResult.HostPenaltyAmount,
                    penaltyResult.Currency,
                    Reason = "host_compensation",
                    CreatedOnUtc = utcNow
                });
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
