using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Abstractions.Payments;
using Bookify.Application.Bookings.ConfirmBooking;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking.Events;

public class BookingConfirmedDomainEventHandlerTests
{
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUserRepository _userRepositoryMock;
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly IPaymentGateway _paymentGatewayMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IEmailService _emailServiceMock;
    private readonly IEmailTemplateService _emailTemplateServiceMock;
    private readonly ILogger<BookingConfirmedDomainEventHandler> _loggerMock;
    private readonly BookingConfirmedDomainEventHandler _handler;

    public BookingConfirmedDomainEventHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _paymentGatewayMock = Substitute.For<IPaymentGateway>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _emailServiceMock = Substitute.For<IEmailService>();
        _emailTemplateServiceMock = Substitute.For<IEmailTemplateService>();
        _loggerMock = Substitute.For<ILogger<BookingConfirmedDomainEventHandler>>();

        var options = Microsoft.Extensions.Options.Options.Create(new BookifyAppOptions
        {
            FrontendUrl = new Uri("https://test.bookify.com")
        });

        _handler = new BookingConfirmedDomainEventHandler(
            _bookingRepositoryMock,
            _userRepositoryMock,
            _transactionRepositoryMock,
            _paymentGatewayMock,
            _dateTimeProviderMock,
            _unitOfWorkMock,
            _emailServiceMock,
            _emailTemplateServiceMock,
            _loggerMock,
            options);
    }

    private static User CreateUser()
    {
        var firstName = new FirstName("Test");
        var lastName = new LastName("User");
        var email = new Email("test@test.com");
        var dateOfBirth = DateOfBirth.Create(new DateOnly(2000, 1, 1));

        return User.Create(firstName, lastName, email, dateOfBirth, Role.Host);
    }

    private static Apartment CreateApartment() =>
        new(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            new Name("Luxury Apartment"),
            new Description("Luxury Apartment Description"),
            new Address("Country", "State", "ZipCode", "City", "Street"),
            new Money(200.00m, Currency.Usd),
            Money.Zero(Currency.Usd),
            [],
            DateTime.UtcNow,
            false,
            null,
            1,
            0);

    private static Bookify.Domain.Bookings.Booking CreateBooking(Guid userId, bool markAsPaid = false)
    {
        var apartment = CreateApartment();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var duration = DateRange.Create(today.AddDays(10), today.AddDays(15));
        var booking = Bookify.Domain.Bookings.Booking.Reserve(
            apartment,
            userId,
            duration,
            DateTime.UtcNow,
            new PricingService());

        if (markAsPaid)
        {
            booking.AuthorizePayment("session-id", "intent-id");
            booking.Confirm(DateTime.UtcNow);
        }

        return booking;
    }

    private static Transaction CreateTransaction(Guid bookingId, string status) =>
        Transaction.Create(
            bookingId,
            "session-id",
            "intent-id",
            "https://checkout.stripe.com/test",
            new Money(1000.00m, Currency.Usd),
            status,
            DateTime.UtcNow);

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
        var booking = CreateBooking(Guid.NewGuid());
        var domainEvent = new BookingConfirmedDomainEvent(booking.Id);

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
        var user = CreateUser();
        var booking = CreateBooking(user.Id);
        var domainEvent = new BookingConfirmedDomainEvent(booking.Id);

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

    [Fact]
    public async Task Handle_ShouldCapturePayment_WhenBookingIsPaidAndNotYetCaptured()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, markAsPaid: true);
        var domainEvent = new BookingConfirmedDomainEvent(booking.Id);
        var transaction = CreateTransaction(booking.Id, "authorized");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(transaction);

        _paymentGatewayMock.CapturePaymentIntentAsync("intent-id", Arg.Any<CancellationToken>())
            .Returns(true);

        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _paymentGatewayMock.Received(1).CapturePaymentIntentAsync("intent-id", Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        transaction.ProviderStatus.Should().Be("paid");
        booking.PaymentStatus.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public async Task Handle_ShouldNotCapturePayment_WhenBookingIsPaidButAlreadyCaptured()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, markAsPaid: true);
        var domainEvent = new BookingConfirmedDomainEvent(booking.Id);
        var transaction = CreateTransaction(booking.Id, "paid");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(transaction);

        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _paymentGatewayMock.DidNotReceiveWithAnyArgs().CapturePaymentIntentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenCaptureFails()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, markAsPaid: true);
        var domainEvent = new BookingConfirmedDomainEvent(booking.Id);
        var transaction = CreateTransaction(booking.Id, "authorized");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(transaction);

        _paymentGatewayMock.CapturePaymentIntentAsync("intent-id", Arg.Any<CancellationToken>())
            .Returns(false);

        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act & Assert
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to capture payment intent intent-id for booking *");

        await _unitOfWorkMock.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
        transaction.ProviderStatus.Should().Be("authorized");
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenTransactionNotFound()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, markAsPaid: true);
        var domainEvent = new BookingConfirmedDomainEvent(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act & Assert
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Transaction not found for booking *");
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenStripePaymentIntentIdIsMissing()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, markAsPaid: true);
        var domainEvent = new BookingConfirmedDomainEvent(booking.Id);
        var transaction = Transaction.Create(
            booking.Id,
            "session-id",
            string.Empty,
            "https://checkout.stripe.com/test",
            new Money(1000.00m, Currency.Usd),
            "authorized",
            DateTime.UtcNow);

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(transaction);

        // Act & Assert
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("StripePaymentIntentId is missing in transaction for booking *");
    }
}
