using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Users;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Bookings.Events;

internal sealed class BookingPaymentAuthorizedDomainEventHandler : INotificationHandler<BookingPaymentAuthorizedDomainEvent>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IJobScheduler _jobScheduler;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly BookifyAppOptions _appOptions;
    private readonly BookingOptions _bookingOptions;

    public BookingPaymentAuthorizedDomainEventHandler(
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IJobScheduler jobScheduler,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        IOptions<BookifyAppOptions> appOptions,
        IOptions<BookingOptions> bookingOptions)
    {
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _jobScheduler = jobScheduler;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _appOptions = appOptions.Value;
        _bookingOptions = bookingOptions.Value;
    }

    public async Task Handle(BookingPaymentAuthorizedDomainEvent notification, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);

        if (booking is null)
        {
            return;
        }

        // Cancel checkout session TTL (TTL1)
        await _jobScheduler.CancelExpireCheckoutSessionAsync(booking.Id, cancellationToken);

        // Schedule host approval TTL (TTL2)
        DateTime utcNow = _dateTimeProvider.UtcNow;
        DateTime expiresAt = utcNow.AddHours(_bookingOptions.HostApprovalTtlHours);
        booking.SetExpiresAt(expiresAt);

        await _jobScheduler.ScheduleExpireHostApprovalAsync(booking.Id, expiresAt, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        User? user = await _userRepository.GetByIdAsync(booking.UserId, cancellationToken);

        if (user is null)
        {
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
            "BookingPaymentAuthorized.html",
            model, cancellationToken);

        var emailMessage = new EmailMessage(
            user.Email.Value,
            "Booking Payment Authorized",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}
