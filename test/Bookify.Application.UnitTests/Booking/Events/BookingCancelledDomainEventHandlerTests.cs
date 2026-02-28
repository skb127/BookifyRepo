using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Bookings.CancelBooking;
using Bookify.Application.Options;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Users;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking.Events;

public class BookingCancelledDomainEventHandlerTests
{
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUserRepository _userRepositoryMock;
    private readonly IEmailService _emailServiceMock;
    private readonly IEmailTemplateService _emailTemplateServiceMock;
    private readonly BookingCancelledDomainEventHandler _handler;

    public BookingCancelledDomainEventHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _emailServiceMock = Substitute.For<IEmailService>();
        _emailTemplateServiceMock = Substitute.For<IEmailTemplateService>();

        var options = Microsoft.Extensions.Options.Options.Create(new BookifyAppOptions
        {
            FrontendUrl = new Uri("https://test.bookify.com")
        });

        _handler = new BookingCancelledDomainEventHandler(
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
        var dateOfBirth = DateOfBirth.Create(new DateOnly(2000, 1, 1))!;

        return User.Create(firstName, lastName, email, dateOfBirth);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenBookingNotFound()
    {
        // Arrange
        var domainEvent = new BookingCancelledDomainEvent(Guid.NewGuid());

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        await _handler.Handle(domainEvent, default);

        // Assert
        await _emailTemplateServiceMock.DidNotReceiveWithAnyArgs().GenerateEmailBodyAsync(default!, default!);
        await _emailServiceMock.DidNotReceiveWithAnyArgs().SendAsync(default!);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenUserNotFound()
    {
        // Arrange
        var domainEvent = new BookingCancelledDomainEvent(Guid.NewGuid());

        // Use reflection to set up a booking without creating complex apartment data from another project
        var booking = (Domain.Bookings.Booking)Activator.CreateInstance(typeof(Domain.Bookings.Booking), true)!;
        typeof(Domain.Bookings.Booking).GetProperty("UserId")!.SetValue(booking, Guid.NewGuid());

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userRepositoryMock.GetByIdAsync(booking.UserId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        await _handler.Handle(domainEvent, default);

        // Assert
        await _emailTemplateServiceMock.DidNotReceiveWithAnyArgs().GenerateEmailBodyAsync(default!, default!);
        await _emailServiceMock.DidNotReceiveWithAnyArgs().SendAsync(default!);
    }

    [Fact]
    public async Task Handle_ShouldSendEmail_WhenValid()
    {
        // Arrange
        var domainEvent = new BookingCancelledDomainEvent(Guid.CreateVersion7());

        var user = CreateUser();

        var booking = (Domain.Bookings.Booking)Activator.CreateInstance(typeof(Domain.Bookings.Booking), true)!;
        typeof(Domain.Bookings.Booking).GetProperty("UserId")!.SetValue(booking, user.Id);

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        string expectedEmailBody = "<html>Email Content</html>";

        _emailTemplateServiceMock.GenerateEmailBodyAsync(
            "BookingCancelled.html",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedEmailBody);

        // Act
        await _handler.Handle(domainEvent, default);

        // Assert
        await _emailTemplateServiceMock.Received(1).GenerateEmailBodyAsync(
            "BookingCancelled.html",
            Arg.Is<object>(m =>
                m.GetType().GetProperty("FirstName")!.GetValue(m)!.ToString() == user.FirstName.Value &&
                m.GetType().GetProperty("HomeUrl")!.GetValue(m)!.ToString() == "https://test.bookify.com/"),
            Arg.Any<CancellationToken>());

        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == user.Email.Value &&
                m.Subject == "Booking Cancelled" &&
                m.Body == expectedEmailBody),
            Arg.Any<CancellationToken>());
    }
}
