using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Bookings.ConfirmBooking;
using Bookify.Application.Options;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Users;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking.Events;

public class BookingConfirmedDomainEventHandlerTests
{
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUserRepository _userRepositoryMock;
    private readonly IEmailService _emailServiceMock;
    private readonly IEmailTemplateService _emailTemplateServiceMock;
    private readonly BookingConfirmedDomainEventHandler _handler;

    public BookingConfirmedDomainEventHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _emailServiceMock = Substitute.For<IEmailService>();
        _emailTemplateServiceMock = Substitute.For<IEmailTemplateService>();

        var options = Microsoft.Extensions.Options.Options.Create(new BookifyAppOptions
        {
            FrontendUrl = new Uri("https://test.bookify.com")
        });

        _handler = new BookingConfirmedDomainEventHandler(
            _bookingRepositoryMock,
            _userRepositoryMock,
            _emailServiceMock,
            _emailTemplateServiceMock,
            options);
    }

    private static User CreateUser()
    {
        var firstName = new FirstName("Test");
        var lastName = new LastName("User");
        var email = new Email("test@test.com");
        var dateOfBirth = DateOfBirth.Create(new DateOnly(2000, 1, 1));

        return User.Create(firstName, lastName, email, dateOfBirth);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenBookingNotFound()
    {
        // Arrange
        var domainEvent = new BookingConfirmedDomainEvent(Guid.NewGuid());

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock.DidNotReceiveWithAnyArgs().GenerateEmailBodyAsync(null!, null!);
        await _emailServiceMock.DidNotReceiveWithAnyArgs().SendAsync(null!);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenUserNotFound()
    {
        // Arrange
        var domainEvent = new BookingConfirmedDomainEvent(Guid.NewGuid());

        var booking = (Domain.Bookings.Booking)Activator.CreateInstance(typeof(Domain.Bookings.Booking), true)!;
        typeof(Domain.Bookings.Booking).GetProperty("UserId")!.SetValue(booking, Guid.NewGuid());

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userRepositoryMock.GetByIdAsync(booking.UserId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock.DidNotReceiveWithAnyArgs().GenerateEmailBodyAsync(null!, null!);
        await _emailServiceMock.DidNotReceiveWithAnyArgs().SendAsync(null!);
    }

    [Fact]
    public async Task Handle_ShouldSendEmail_WhenValid()
    {
        // Arrange
        var domainEvent = new BookingConfirmedDomainEvent(Guid.CreateVersion7());

        var user = CreateUser();

        var booking = (Domain.Bookings.Booking)Activator.CreateInstance(typeof(Domain.Bookings.Booking), true)!;
        typeof(Domain.Bookings.Booking).GetProperty("UserId")!.SetValue(booking, user.Id);
        typeof(Domain.Bookings.Booking).GetProperty("Id")!.SetValue(booking, domainEvent.BookingId);

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        string expectedEmailBody = "<html>Email Content</html>";

        _emailTemplateServiceMock.GenerateEmailBodyAsync(
            "BookingConfirmed.html",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedEmailBody);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock.Received(1).GenerateEmailBodyAsync(
            "BookingConfirmed.html",
            Arg.Is<object>(m =>
                m.GetType().GetProperty("FirstName")!.GetValue(m)!.ToString() == user.FirstName.Value &&
                m.GetType().GetProperty("BookingId")!.GetValue(m)!.ToString() == domainEvent.BookingId.ToString() &&
                m.GetType().GetProperty("HomeUrl")!.GetValue(m)!.ToString() == "https://test.bookify.com/"),
            Arg.Any<CancellationToken>());

        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == user.Email.Value &&
                m.Subject == "Booking Confirmed" &&
                m.Body == expectedEmailBody),
            Arg.Any<CancellationToken>());
    }
}
