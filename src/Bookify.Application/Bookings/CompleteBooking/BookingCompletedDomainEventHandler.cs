using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Users;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Bookings.CompleteBooking;

internal sealed class BookingCompletedDomainEventHandler : INotificationHandler<BookingCompletedDomainEvent>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly BookifyAppOptions _appOptions;

    public BookingCompletedDomainEventHandler(
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        IOptions<BookifyAppOptions> appOptions)
    {
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _appOptions = appOptions.Value;
    }

    public async Task Handle(BookingCompletedDomainEvent notification, CancellationToken cancellationToken)
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
            BookingId = booking.Id.ToString(),
            HomeUrl = homeUri.AbsoluteUri
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "BookingCompleted.html",
            model, cancellationToken);

        var emailMessage = new EmailMessage(
            user.Email.Value,
            "Stay Completed",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);

        // Mark the booking as notified to prevent the batch notification job
        // from sending a duplicate email for this booking
        booking.MarkCompletionNotified(_dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
