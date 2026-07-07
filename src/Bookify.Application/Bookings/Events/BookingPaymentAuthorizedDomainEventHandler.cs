using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
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
    private readonly IApartmentRepository _apartmentRepository;
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
        IApartmentRepository apartmentRepository,
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
        _apartmentRepository = apartmentRepository;
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
        DateTime standardExpiry = utcNow.AddHours(_bookingOptions.HostApprovalTtlHours);
        DateTime checkInDayEnd = booking.Duration.Start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);
        DateTime expiresAt = standardExpiry < checkInDayEnd ? standardExpiry : checkInDayEnd;

        booking.SetExpiresAt(expiresAt);

        await _jobScheduler.ScheduleExpireHostApprovalAsync(booking.Id, expiresAt, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        User? user = await _userRepository.GetByIdAsync(booking.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        // 1. Send Guest Email
        Uri homeUri = _appOptions.FrontendUrl;

        object guestModel = new
        {
            FirstName = user.FirstName.Value,
            BookingId = booking.Id.ToString(),
            HomeUrl = homeUri.AbsoluteUri
        };

        string guestEmailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "BookingPaymentAuthorized.html",
            guestModel, cancellationToken);

        var guestEmailMessage = new EmailMessage(
            user.Email.Value,
            "Booking Payment Authorized",
            guestEmailBody);

        await _emailService.SendAsync(guestEmailMessage, cancellationToken);

        // 2. Send Host Email
        Apartment? apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId, cancellationToken);
        if (apartment is null)
        {
            return;
        }

        User? host = await _userRepository.GetByIdAsync(apartment.OwnerId, cancellationToken);
        if (host is null)
        {
            return;
        }

        var today = DateOnly.FromDateTime(utcNow);
        bool isUrgent = booking.Duration.Start == today || booking.Duration.Start == today.AddDays(1);
        string hostSubject = isUrgent ? "🚨 URGENT: Booking Approval Required" : "Booking Approval Required";

        object hostModel = new
        {
            FirstName = host.FirstName.Value,
            GuestName = $"{user.FirstName.Value} {user.LastName.Value}",
            BookingId = booking.Id.ToString(),
            CheckInDate = booking.Duration.Start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            CheckOutDate = booking.Duration.End.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            IsUrgent = isUrgent,
            HomeUrl = homeUri.AbsoluteUri
        };

        string hostEmailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "BookingApprovalRequired.html",
            hostModel, cancellationToken);

        var hostEmailMessage = new EmailMessage(
            host.Email.Value,
            hostSubject,
            hostEmailBody);

        await _emailService.SendAsync(hostEmailMessage, cancellationToken);
    }
}
