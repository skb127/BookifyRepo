using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Payments.FailRefund;

internal sealed class FailRefundCommandHandler : ICommandHandler<FailRefundCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FailRefundCommandHandler> _logger;

    public FailRefundCommandHandler(
        IBookingRepository bookingRepository,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        ILogger<FailRefundCommandHandler> logger)
    {
        _bookingRepository = bookingRepository;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(FailRefundCommand request, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        // Idempotency: If the payment status is not RefundProcessing, return success
        if (booking.PaymentStatus != PaymentStatus.RefundProcessing)
        {
            return Result.Success();
        }

        DateTime utcNow = _dateTimeProvider.UtcNow;

        Result result = booking.RevertRefundFailure(utcNow);
        if (result.IsFailure)
        {
            return result;
        }

        _logger.LogError(
            "Refund failed for Booking {BookingId}. Stripe Refund ID: {StripeRefundId}. Reason: {FailureReason}",
            request.BookingId,
            request.StripeRefundId,
            request.FailureReason);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
