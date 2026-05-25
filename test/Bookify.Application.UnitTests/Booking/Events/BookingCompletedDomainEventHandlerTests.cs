using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Bookings.CompleteBooking;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Users;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using FluentAssertions;

namespace Bookify.Application.UnitTests.Booking.Events;

public class BookingCompletedDomainEventHandlerTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;

    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUserRepository _userRepositoryMock;
    private readonly IEmailService _emailServiceMock;
    private readonly IEmailTemplateService _emailTemplateServiceMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly BookingCompletedDomainEventHandler _handler;

    public BookingCompletedDomainEventHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _emailServiceMock = Substitute.For<IEmailService>();
        _emailTemplateServiceMock = Substitute.For<IEmailTemplateService>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        var options = Microsoft.Extensions.Options.Options.Create(new BookifyAppOptions
        {
            FrontendUrl = new Uri("https://test.bookify.com")
        });

        _handler = new BookingCompletedDomainEventHandler(
            _bookingRepositoryMock,
            _userRepositoryMock,
            _emailServiceMock,
            _emailTemplateServiceMock,
            _dateTimeProviderMock,
            _unitOfWorkMock,
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
        var domainEvent = new BookingCompletedDomainEvent(Guid.NewGuid());

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        await _handler.Handle(domainEvent, default);

        // Assert
        await _emailTemplateServiceMock.DidNotReceiveWithAnyArgs().GenerateEmailBodyAsync(default!, default!);
        await _emailServiceMock.DidNotReceiveWithAnyArgs().SendAsync(default!);
        await _unitOfWorkMock.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenUserNotFound()
    {
        // Arrange
        var domainEvent = new BookingCompletedDomainEvent(Guid.NewGuid());

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
        await _unitOfWorkMock.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ShouldSendEmailAndMarkAsNotified_WhenValid()
    {
        // Arrange
        var domainEvent = new BookingCompletedDomainEvent(Guid.CreateVersion7());

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
                "BookingCompleted.html",
                Arg.Any<object>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedEmailBody);

        // Act
        await _handler.Handle(domainEvent, default);

        // Assert
        await _emailTemplateServiceMock.Received(1).GenerateEmailBodyAsync(
            "BookingCompleted.html",
            Arg.Is<object>(m =>
                m.GetType().GetProperty("FirstName")!.GetValue(m)!.ToString() == user.FirstName.Value &&
                m.GetType().GetProperty("BookingId")!.GetValue(m)!.ToString() == domainEvent.BookingId.ToString() &&
                m.GetType().GetProperty("HomeUrl")!.GetValue(m)!.ToString() == "https://test.bookify.com/"),
            Arg.Any<CancellationToken>());

        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == user.Email.Value &&
                m.Subject == $"Stay Completed - {domainEvent.BookingId}" &&
                m.Body == expectedEmailBody),
            Arg.Any<CancellationToken>());

        // Verify the booking was marked as notified to prevent duplicates from the batch job
        booking.CompletedNotificationSentAt.Should().Be(UtcNow);

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
