using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;

namespace Bookify.Application.Payments.ExpireCheckoutSession;

internal sealed class ExpireStripeSessionCommandHandler : ICommandHandler<ExpireStripeSessionCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IJobScheduler _jobScheduler;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ExpireStripeSessionCommandHandler(
        IBookingRepository bookingRepository,
        ITransactionRepository transactionRepository,
        IJobScheduler jobScheduler,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _bookingRepository = bookingRepository;
        _transactionRepository = transactionRepository;
        _jobScheduler = jobScheduler;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ExpireStripeSessionCommand request, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        // Idempotency: If booking is already expired, return success
        if (booking.Status == BookingStatus.Expired)
        {
            return Result.Success();
        }

        DateTime utcNow = _dateTimeProvider.UtcNow;

        Result result = booking.Expire(utcNow);
        if (result.IsFailure)
        {
            return result;
        }

        // Cancel checkout session expiration TTL
        await _jobScheduler.CancelExpireCheckoutSessionAsync(booking.Id, cancellationToken);

        // Update transaction status
        Transaction? transaction = await _transactionRepository.GetByStripeSessionIdAsync(request.StripeSessionId, cancellationToken);
        transaction?.UpdateStatus("expired", null, utcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
