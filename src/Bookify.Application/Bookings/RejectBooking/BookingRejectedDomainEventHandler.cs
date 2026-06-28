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

namespace Bookify.Application.Bookings.RejectBooking;

internal sealed class BookingRejectedDomainEventHandler : INotificationHandler<BookingRejectedDomainEvent>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ILogger<BookingRejectedDomainEventHandler> _logger;
    private readonly BookifyAppOptions _appOptions;

    public BookingRejectedDomainEventHandler(
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        IPaymentGateway paymentGateway,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        ILogger<BookingRejectedDomainEventHandler> logger,
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

    public async Task Handle(BookingRejectedDomainEvent notification, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);

        if (booking is null)
        {
            _logger.LogWarning("Booking {BookingId} not found during rejection event handling.", notification.BookingId);
            return;
        }

        if (booking.PaymentStatus == PaymentStatus.AuthorizationReleased)
        {
            Transaction? transaction = await _transactionRepository.GetByBookingIdAsync(booking.Id, cancellationToken);
            if (transaction is null)
            {
                _logger.LogError("Transaction not found for rejected booking {BookingId}.", booking.Id);
                throw new InvalidOperationException($"Transaction not found for rejected booking {booking.Id}");
            }

            if (transaction.ProviderStatus != "canceled")
            {
                if (!string.IsNullOrEmpty(transaction.StripePaymentIntentId))
                {
                    _logger.LogInformation("Cancelling payment intent {PaymentIntentId} for rejected booking {BookingId}.",
                        transaction.StripePaymentIntentId, booking.Id);

                    bool canceled = await _paymentGateway.CancelPaymentIntentAsync(transaction.StripePaymentIntentId, cancellationToken);
                    if (canceled)
                    {
                        transaction.UpdateStatus("canceled", transaction.StripePaymentIntentId, _dateTimeProvider.UtcNow);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Successfully cancelled payment intent {PaymentIntentId} for booking {BookingId}.",
                            transaction.StripePaymentIntentId, booking.Id);
                    }
                    else
                    {
                        _logger.LogError("Failed to cancel payment intent {PaymentIntentId} for booking {BookingId}.",
                            transaction.StripePaymentIntentId, booking.Id);
                        throw new InvalidOperationException($"Failed to release payment intent authorization {transaction.StripePaymentIntentId} for booking {booking.Id}");
                    }
                }
                else
                {
                    _logger.LogWarning("StripePaymentIntentId is missing in transaction for rejected booking {BookingId}. Skipping Stripe cancel.",
                        booking.Id);
                }
            }
        }

        User? user = await _userRepository.GetByIdAsync(booking.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("User {UserId} not found for booking {BookingId} during rejection email sending.", booking.UserId, booking.Id);
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
            "BookingRejected.html",
            model, cancellationToken);

        var emailMessage = new EmailMessage(
            user.Email.Value,
            "Booking Rejected",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}
