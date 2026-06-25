using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;

namespace Bookify.Application.Payments.ConfirmPayment;

internal sealed class ConfirmPaymentCommandHandler : ICommandHandler<ConfirmPaymentCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IJobScheduler _jobScheduler;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmPaymentCommandHandler(
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

    public async Task<Result> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        // Idempotency: If booking is already confirmed or reserved (for manual authorization), return Success
        if (booking.Status == BookingStatus.Confirmed || 
            !request.IsInstantBooking && booking.Status == BookingStatus.Reserved)
        {
            return Result.Success();
        }

        DateTime utcNow = _dateTimeProvider.UtcNow;

        Result transitionResult = request.IsInstantBooking
            ? booking.MarkAsPaid(request.StripePaymentIntentId, utcNow)
            : booking.AuthorizePayment(request.StripeSessionId, request.StripePaymentIntentId);

        if (transitionResult.IsFailure)
        {
            return transitionResult;
        }

        // Cancel the checkout session expiration TTL
        await _jobScheduler.CancelExpireCheckoutSessionAsync(booking.Id, cancellationToken);

        // Manage Transaction status
        Transaction? transaction = await _transactionRepository.GetByStripeSessionIdAsync(request.StripeSessionId, cancellationToken);
        string providerStatus = request.IsInstantBooking ? "paid" : "authorized";

        if (transaction is null)
        {
            transaction = Transaction.Create(
                booking.Id,
                request.StripeSessionId,
                request.StripePaymentIntentId,
                string.Empty,
                booking.TotalPrice with { },
                providerStatus,
                utcNow);

            _transactionRepository.Add(transaction);
        }
        else
        {
            transaction.UpdateStatus(providerStatus, request.StripePaymentIntentId, utcNow);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
