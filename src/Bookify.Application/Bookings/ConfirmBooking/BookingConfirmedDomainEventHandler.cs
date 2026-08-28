using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Abstractions.Payments;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Users;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Bookings.ConfirmBooking;

internal sealed class BookingConfirmedDomainEventHandler : INotificationHandler<BookingConfirmedDomainEvent>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ILogger<BookingConfirmedDomainEventHandler> _logger;
    private readonly BookifyAppOptions _appOptions;

    public BookingConfirmedDomainEventHandler(
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IPaymentGateway paymentGateway,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        ILogger<BookingConfirmedDomainEventHandler> logger,
        IOptions<BookifyAppOptions> appOptions)
    {
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _paymentGateway = paymentGateway;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _logger = logger;
        _appOptions = appOptions.Value;
    }

    public async Task Handle(BookingConfirmedDomainEvent notification, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);

        if (booking is null)
        {
            _logger.LogWarning("Booking {BookingId} not found during confirmation event handling.",
                notification.BookingId);
            return;
        }

        if (booking.PaymentStatus == PaymentStatus.Authorized || booking.PaymentStatus == PaymentStatus.Paid)
        {
            Transaction? transaction = await _transactionRepository.GetByBookingIdAsync(booking.Id, cancellationToken);
            if (transaction is null)
            {
                _logger.LogError("Transaction not found for booking {BookingId}.", booking.Id);
                throw new InvalidOperationException($"Transaction not found for booking {booking.Id}");
            }

            if (transaction.ProviderStatus != "paid")
            {
                if (string.IsNullOrEmpty(transaction.StripePaymentIntentId))
                {
                    _logger.LogError("StripePaymentIntentId is missing in transaction for booking {BookingId}.", booking.Id);
                    throw new InvalidOperationException($"StripePaymentIntentId is missing in transaction for booking {booking.Id}");
                }

                _logger.LogInformation("Capturing payment intent {PaymentIntentId} for booking {BookingId}.",
                    transaction.StripePaymentIntentId, booking.Id);

                bool captured =
                    await _paymentGateway.CapturePaymentIntentAsync(transaction.StripePaymentIntentId,
                        cancellationToken);
                if (captured)
                {
                    transaction.UpdateStatus("paid", transaction.StripePaymentIntentId, _dateTimeProvider.UtcNow);
                    booking.CompletePayment(transaction.StripePaymentIntentId);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation(
                        "Successfully captured payment intent {PaymentIntentId} for booking {BookingId}.",
                        transaction.StripePaymentIntentId, booking.Id);
                }
                else
                {
                    _logger.LogError("Failed to capture payment intent {PaymentIntentId} for booking {BookingId}.",
                        transaction.StripePaymentIntentId, booking.Id);
                    throw new InvalidOperationException(
                        $"Failed to capture payment intent {transaction.StripePaymentIntentId} for booking {booking.Id}");
                }
            }
        }

        User? user = await _userRepository.GetByIdAsync(booking.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("User {UserId} not found for booking {BookingId} during confirmation email sending.",
                booking.UserId, booking.Id);
            return;
        }

        Uri homeUri = _appOptions.FrontendUrl;

        var model = new
        {
            FirstName = user.FirstName.Value,
            BookingId = booking.Id.ToString(),
            HomeUrl = homeUri.AbsoluteUri
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "BookingConfirmed.html",
            model, cancellationToken);

        var emailMessage = new EmailMessage(
            user.Email.Value,
            "Booking Confirmed",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}
