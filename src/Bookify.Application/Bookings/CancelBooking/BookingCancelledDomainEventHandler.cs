using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Abstractions.Payments;
using Bookify.Application.Options;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Users;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Bookify.Domain.Abstractions;

namespace Bookify.Application.Bookings.CancelBooking;

internal sealed class BookingCancelledDomainEventHandler : INotificationHandler<BookingCancelledDomainEvent>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IApartmentRepository _apartmentRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ILogger<BookingCancelledDomainEventHandler> _logger;
    private readonly BookifyAppOptions _appOptions;

    public BookingCancelledDomainEventHandler(
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        IApartmentRepository apartmentRepository,
        ITransactionRepository transactionRepository,
        IPaymentGateway paymentGateway,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        ILogger<BookingCancelledDomainEventHandler> logger,
        IOptions<BookifyAppOptions> appOptions)
    {
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _apartmentRepository = apartmentRepository;
        _transactionRepository = transactionRepository;
        _paymentGateway = paymentGateway;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _logger = logger;
        _appOptions = appOptions.Value;
    }

    public async Task Handle(BookingCancelledDomainEvent notification, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);

        if (booking is null)
        {
            _logger.LogWarning("Booking {BookingId} not found during cancellation event handling.",
                notification.BookingId);
            return;
        }

        // Bypass notifications and payment gateway operations if booking was cancelled in PendingPayment (Unpaid)
        if (booking.PaymentStatus == PaymentStatus.Unpaid)
        {
            _logger.LogInformation(
                "Booking {BookingId} was cancelled while in PendingPayment. Skipping payment gateway operations and email notifications.",
                booking.Id);
            return;
        }

        // Handle Stripe refund or release
        if (booking.PaymentStatus == PaymentStatus.AuthorizationReleased)
        {
            Transaction? transaction = await _transactionRepository.GetByBookingIdAsync(booking.Id, cancellationToken);
            if (transaction is null)
            {
                _logger.LogError(
                    "Transaction not found for cancelled booking {BookingId} with AuthorizationReleased payment status.",
                    booking.Id);
                throw new InvalidOperationException($"Transaction not found for cancelled booking {booking.Id}");
            }

            if (transaction.ProviderStatus != "canceled")
            {
                if (!string.IsNullOrEmpty(transaction.StripePaymentIntentId))
                {
                    _logger.LogInformation("Cancelling payment intent {PaymentIntentId} for booking {BookingId}.",
                        transaction.StripePaymentIntentId, booking.Id);
                    bool canceled = await _paymentGateway.CancelPaymentIntentAsync(transaction.StripePaymentIntentId,
                        cancellationToken);

                    if (canceled)
                    {
                        transaction.UpdateStatus("canceled", transaction.StripePaymentIntentId,
                            _dateTimeProvider.UtcNow);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation(
                            "Successfully cancelled payment intent {PaymentIntentId} for booking {BookingId}.",
                            transaction.StripePaymentIntentId, booking.Id);
                    }
                    else
                    {
                        _logger.LogError("Failed to cancel payment intent {PaymentIntentId} for booking {BookingId}.",
                            transaction.StripePaymentIntentId, booking.Id);
                        throw new InvalidOperationException(
                            $"Failed to release payment intent authorization {transaction.StripePaymentIntentId} for booking {booking.Id}");
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "StripePaymentIntentId is missing in transaction for authorized booking {BookingId}. Skipping Stripe cancellation.",
                        booking.Id);
                }
            }
        }
        else if (booking.PaymentStatus == PaymentStatus.Paid && notification.RefundAmount > 0)
        {
            Transaction? transaction = await _transactionRepository.GetByBookingIdAsync(booking.Id, cancellationToken);
            if (transaction is null)
            {
                _logger.LogError("Transaction not found for cancelled paid booking {BookingId} to initiate refund.",
                    booking.Id);
                throw new InvalidOperationException($"Transaction not found for cancelled paid booking {booking.Id}");
            }

            if (!string.IsNullOrEmpty(transaction.StripePaymentIntentId))
            {
                string refundReason = notification.CancelledByHost ? "Cancelled by Host" : "Cancelled by Guest";

                // Update payment status to RefundProcessing in Domain
                Result initiateRefundResult = booking.InitiateRefund(
                    notification.RefundAmount.Value,
                    notification.Currency ?? "USD",
                    refundReason,
                    _dateTimeProvider.UtcNow);

                if (initiateRefundResult.IsSuccess)
                {
                    _logger.LogInformation(
                        "Initiating refund of {Amount} {Currency} for payment intent {PaymentIntentId} (Booking {BookingId}).",
                        notification.RefundAmount.Value, notification.Currency, transaction.StripePaymentIntentId,
                        booking.Id);

                    // Trigger Stripe refund
                    await _paymentGateway.CreateRefundAsync(
                        transaction.StripePaymentIntentId,
                        notification.RefundAmount.Value,
                        notification.Currency ?? "USD",
                        cancellationToken);

                    // Save the local payment status change (RefundProcessing)
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    _logger.LogError("Failed to initiate refund for booking {BookingId}: {Error}", booking.Id,
                        initiateRefundResult.Error);
                }
            }
            else
            {
                _logger.LogError(
                    "StripePaymentIntentId is missing in transaction for paid booking {BookingId}. Skipping Stripe refund.",
                    booking.Id);
            }
        }

        // Fetch required entities for emails
        Apartment? apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId, cancellationToken);
        User? guest = await _userRepository.GetByIdAsync(booking.UserId, cancellationToken);
        User? host = apartment is not null
            ? await _userRepository.GetByIdAsync(apartment.OwnerId, cancellationToken)
            : null;

        // Send email to guest
        if (guest is not null && apartment is not null)
        {
            Uri homeUri = _appOptions.FrontendUrl;
            string templateName = notification.CancelledByHost
                ? "BookingCancelledGuestByHost.html"
                : "BookingCancelled.html";
            string subject = notification.CancelledByHost ? "Booking Cancelled by Host" : "Booking Cancelled";

            decimal refundAmount = booking.PaymentStatus == PaymentStatus.RefundProcessing
                ? notification.RefundAmount ?? 0
                : 0;
            decimal guestPenaltyAmount = booking.PaymentStatus == PaymentStatus.RefundProcessing
                ? Math.Max(0m, booking.TotalPrice.Amount - refundAmount)
                : 0;
            bool showCancellationDetails = guestPenaltyAmount > 0;
            bool wasPaid = booking.PaymentStatus == PaymentStatus.RefundProcessing;

            var guestModel = new
            {
                FirstName = guest.FirstName.Value,
                ApartmentName = apartment.Name.Value,
                BookingDates = $"{booking.Duration.Start:dd/MM/yyyy} - {booking.Duration.End:dd/MM/yyyy}",
                TotalPrice = booking.TotalPrice.Amount,
                Currency = booking.TotalPrice.Currency.Code,
                RefundAmount = refundAmount,
                CancellationFee = Math.Round(guestPenaltyAmount, 2),
                ShowCancellationDetails = showCancellationDetails,
                WasPaid = wasPaid,
                HomeUrl = homeUri.AbsoluteUri
            };

            string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
                templateName,
                guestModel,
                cancellationToken);

            var emailMessage = new EmailMessage(
                guest.Email.Value,
                subject,
                emailBody);

            await _emailService.SendAsync(emailMessage, cancellationToken);
        }

        // Send email to host
        if (host is not null && apartment is not null)
        {
            Uri homeUri = _appOptions.FrontendUrl;
            string templateName;
            string subject;
            object hostModel;

            if (notification.CancelledByHost)
            {
                templateName = "BookingCancelledHost.html";
                subject = "Booking Cancelled - Penalty Applied";
                hostModel = new
                {
                    FirstName = host.FirstName.Value,
                    ApartmentName = apartment.Name.Value,
                    BookingDates = $"{booking.Duration.Start:dd/MM/yyyy} - {booking.Duration.End:dd/MM/yyyy}",
                    PenaltyAmount = notification.HostPenaltyAmount,
                    Currency = notification.Currency ?? "USD",
                    HomeUrl = homeUri.AbsoluteUri
                };
            }
            else
            {
                decimal guestPenaltyAmount = booking.TotalPrice.Amount - (notification.RefundAmount ?? 0);
                templateName = "BookingCancelledHostByGuest.html";
                subject = "Booking Cancelled by Guest";
                hostModel = new
                {
                    FirstName = host.FirstName.Value,
                    ApartmentName = apartment.Name.Value,
                    BookingDates = $"{booking.Duration.Start:dd/MM/yyyy} - {booking.Duration.End:dd/MM/yyyy}",
                    PenaltyAmount = Math.Max(0m, Math.Round(guestPenaltyAmount, 2)),
                    Currency = booking.TotalPrice.Currency.Code,
                    HomeUrl = homeUri.AbsoluteUri
                };
            }

            string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
                templateName,
                hostModel,
                cancellationToken);

            var emailMessage = new EmailMessage(
                host.Email.Value,
                subject,
                emailBody);

            await _emailService.SendAsync(emailMessage, cancellationToken);
        }
    }
}
