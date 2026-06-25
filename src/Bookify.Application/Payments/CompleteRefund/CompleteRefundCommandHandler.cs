using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;

namespace Bookify.Application.Payments.CompleteRefund;

internal sealed class CompleteRefundCommandHandler : ICommandHandler<CompleteRefundCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteRefundCommandHandler(
        IBookingRepository bookingRepository,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _bookingRepository = bookingRepository;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CompleteRefundCommand request, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        // Idempotency: If already refunded, return success
        if (booking.PaymentStatus == PaymentStatus.Refunded)
        {
            return Result.Success();
        }

        DateTime utcNow = _dateTimeProvider.UtcNow;

        Result result = booking.CompleteRefund(utcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
