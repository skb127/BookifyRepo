using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Users;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Bookings.CancelBooking;

internal sealed class BookingCancelledDomainEventHandler : INotificationHandler<BookingCancelledDomainEvent>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly BookifyAppOptions _appOptions;

    public BookingCancelledDomainEventHandler(
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IOptions<BookifyAppOptions> appOptions)
    {
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _appOptions = appOptions.Value;
    }

    public async Task Handle(BookingCancelledDomainEvent notification, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);

        if (booking is null)
        {
            return;
        }

        User? user = await _userRepository.GetByIdAsync(booking.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        Uri homeUri = _appOptions.FrontendUrl;

        var model = new
        {
            FirstName = user.FirstName.Value,
            HomeUrl = homeUri.AbsoluteUri
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "BookingCancelled.html",
            model, cancellationToken);

        var emailMessage = new EmailMessage(
            user.Email.Value,
            "Booking Cancelled",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}
