using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Abstractions.Payments;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;

namespace Bookify.Application.Bookings.ExpireHostApproval;

internal sealed class ExpireHostApprovalCommandHandler : ICommandHandler<ExpireHostApprovalCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ExpireHostApprovalCommandHandler(
        IBookingRepository bookingRepository,
        ITransactionRepository transactionRepository,
        IPaymentGateway paymentGateway,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _bookingRepository = bookingRepository;
        _transactionRepository = transactionRepository;
        _paymentGateway = paymentGateway;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ExpireHostApprovalCommand request, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        if (booking.Status != BookingStatus.Reserved)
        {
            return Result.Success();
        }

        DateTime utcNow = _dateTimeProvider.UtcNow;
        Result result = booking.Expire(utcNow);

        if (result.IsFailure)
        {
            return result;
        }

        if (booking.PaymentStatus == PaymentStatus.AuthorizationReleased)
        {
            Transaction? transaction = await _transactionRepository.GetByBookingIdAsync(booking.Id, cancellationToken);
            if (transaction?.StripePaymentIntentId is not null)
            {
                await _paymentGateway.CancelPaymentIntentAsync(transaction.StripePaymentIntentId, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
