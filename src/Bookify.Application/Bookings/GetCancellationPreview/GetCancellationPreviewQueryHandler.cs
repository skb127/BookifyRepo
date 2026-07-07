using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.CancellationPolicies;

namespace Bookify.Application.Bookings.GetCancellationPreview;

internal sealed class GetCancellationPreviewQueryHandler : IQueryHandler<GetCancellationPreviewQuery, CancellationPreviewResponse>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly ICancellationPolicyRepository _cancellationPolicyRepository;
    private readonly IUserContext _userContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetCancellationPreviewQueryHandler(
        IBookingRepository bookingRepository,
        IApartmentRepository apartmentRepository,
        ICancellationPolicyRepository cancellationPolicyRepository,
        IUserContext userContext,
        IDateTimeProvider dateTimeProvider)
    {
        _bookingRepository = bookingRepository;
        _apartmentRepository = apartmentRepository;
        _cancellationPolicyRepository = cancellationPolicyRepository;
        _userContext = userContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<CancellationPreviewResponse>> Handle(
        GetCancellationPreviewQuery request,
        CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null)
        {
            return Result.Failure<CancellationPreviewResponse>(BookingErrors.NotFound);
        }

        Apartment? apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId, cancellationToken);
        if (apartment is null)
        {
            return Result.Failure<CancellationPreviewResponse>(ApartmentErrors.NotFound);
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
            return Result.Failure<CancellationPreviewResponse>(BookingErrors.Unauthorized);
        }

        // Validate cancellability status check
        if (booking.Status != BookingStatus.Reserved && booking.Status != BookingStatus.Confirmed &&
            booking.Status != BookingStatus.PendingPayment || cancelledByHost && booking.Status == BookingStatus.PendingPayment)
        {
            return Result.Failure<CancellationPreviewResponse>(BookingErrors.NotCancellable);
        }

        DateTime utcNow = _dateTimeProvider.UtcNow;

        if (booking.Status == BookingStatus.Confirmed)
        {
            var currentDate = DateOnly.FromDateTime(utcNow);
            if (currentDate > booking.Duration.Start)
            {
                return Result.Failure<CancellationPreviewResponse>(BookingErrors.AlreadyStarted);
            }
        }

        // Calculate preview
        if (booking.PaymentStatus != PaymentStatus.Paid)
        {
            // No policy is needed for unpaid/authorized bookings, they have 0 penalty
            return new CancellationPreviewResponse(
                booking.TotalPrice.Amount,
                0m,
                0m,
                booking.TotalPrice.Currency.Code,
                false,
                false,
                "None",
                cancelledByHost);
        }

        CancellationPolicy? policy = apartment.CancellationPolicyId.HasValue
            ? await _cancellationPolicyRepository.GetByIdAsync(apartment.CancellationPolicyId.Value, cancellationToken)
            : await _cancellationPolicyRepository.GetDefaultAsync(cancellationToken);
            
        if (policy is null)
        {
            return Result.Failure<CancellationPreviewResponse>(BookingErrors.NoPolicyAvailable);
        }

        var engine = new CancellationPolicyEngine();
        PenaltyResult penaltyResult = engine.CalculatePenalty(booking, policy, utcNow, cancelledByHost);

        var checkInDateTime = booking.Duration.Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        double hoursUntilCheckin = (checkInDateTime - utcNow).TotalHours;
        bool isLate = hoursUntilCheckin < policy.ThresholdHours;

        decimal penaltyAmount = cancelledByHost ? penaltyResult.HostPenaltyAmount : penaltyResult.GuestPenaltyAmount;

        return new CancellationPreviewResponse(
            booking.TotalPrice.Amount,
            penaltyAmount,
            penaltyResult.RefundAmount,
            penaltyResult.Currency,
            penaltyResult.RequiresRefund,
            isLate,
            policy.Name,
            cancelledByHost);
    }
}
