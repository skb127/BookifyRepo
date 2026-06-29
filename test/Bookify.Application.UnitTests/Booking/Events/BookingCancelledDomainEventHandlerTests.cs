using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Abstractions.Payments;
using Bookify.Application.Bookings.CancelBooking;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.CancellationPolicies;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking.Events;

public class BookingCancelledDomainEventHandlerTests
{
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUserRepository _userRepositoryMock;
    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly IPaymentGateway _paymentGatewayMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IEmailService _emailServiceMock;
    private readonly IEmailTemplateService _emailTemplateServiceMock;
    private readonly ILogger<BookingCancelledDomainEventHandler> _loggerMock;
    private readonly BookingCancelledDomainEventHandler _handler;

    public BookingCancelledDomainEventHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _paymentGatewayMock = Substitute.For<IPaymentGateway>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _emailServiceMock = Substitute.For<IEmailService>();
        _emailTemplateServiceMock = Substitute.For<IEmailTemplateService>();
        _loggerMock = Substitute.For<ILogger<BookingCancelledDomainEventHandler>>();

        var options = Microsoft.Extensions.Options.Options.Create(new BookifyAppOptions
        {
            FrontendUrl = new Uri("https://test.bookify.com")
        });

        _handler = new BookingCancelledDomainEventHandler(
            _bookingRepositoryMock,
            _userRepositoryMock,
            _apartmentRepositoryMock,
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

        return User.Create(firstName, lastName, email, dateOfBirth);
    }

    private static Apartment CreateApartment(Guid? ownerId = null) =>
        new(
            Guid.CreateVersion7(),
            ownerId ?? Guid.NewGuid(),
            new Name("Luxury Apartment"),
            new Description("Luxury Apartment Description"),
            new Address("Country", "State", "ZipCode", "City", "Street"),
            new Money(200.00m, Currency.Usd),
            Money.Zero(Currency.Usd),
            [],
            DateTime.UtcNow,
            false);

    private static Transaction CreateTransaction(Guid bookingId, string? paymentIntentId) =>
        Transaction.Create(
            bookingId,
            "session-id",
            paymentIntentId,
            "https://checkout.stripe.com/test",
            new Money(1000.00m, Currency.Usd),
            "paid",
            DateTime.UtcNow);

    private static Bookify.Domain.Bookings.Booking CreateBooking(Guid userId, PaymentStatus paymentStatus)
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

        if (paymentStatus == PaymentStatus.Paid || paymentStatus == PaymentStatus.RefundProcessing)
        {
            booking.MarkAsPaid("pi_test_intent", DateTime.UtcNow);
            
            var policy = CancellationPolicy.Create("Standard", 0.0m, 0.0m, 0.0m, 0.0m, 24, true, DateTime.UtcNow);
            var engine = new CancellationPolicyEngine();
            booking.Cancel(DateTime.UtcNow, policy, engine, false);
            
            if (paymentStatus == PaymentStatus.RefundProcessing)
            {
                booking.InitiateRefund(0m, "USD", "Test");
            }
        }
        else if (paymentStatus == PaymentStatus.Authorized)
        {
            booking.AuthorizePayment("session-id", "intent-id");
        }
        else if (paymentStatus == PaymentStatus.AuthorizationReleased)
        {
            booking.AuthorizePayment("session-id", "intent-id");
            booking.Cancel(DateTime.UtcNow, null, null, false);
        }
        else if (paymentStatus == PaymentStatus.Unpaid)
        {
            booking.Cancel(DateTime.UtcNow, null, null, false);
        }

        return booking;
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenBookingNotFound()
    {
        // Arrange
        var domainEvent = new BookingCancelledDomainEvent(Guid.NewGuid());

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock.DidNotReceiveWithAnyArgs().GenerateEmailBodyAsync(default!, default!);
        await _emailServiceMock.DidNotReceiveWithAnyArgs().SendAsync(default!);
    }

    [Fact]
    public async Task Handle_ShouldSendEmailToGuest_WhenValid()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, PaymentStatus.Authorized);
        var domainEvent = new BookingCancelledDomainEvent(booking.Id);
        var apartment = CreateApartment();

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        string expectedEmailBody = "<html>Email Content</html>";

        _emailTemplateServiceMock.GenerateEmailBodyAsync(
                "BookingCancelled.html",
                Arg.Is<object>(m =>
                    m.GetType().GetProperty("FirstName")!.GetValue(m)!.ToString() == user.FirstName.Value &&
                    m.GetType().GetProperty("ApartmentName")!.GetValue(m)!.ToString() == apartment.Name.Value &&
                    !(bool)m.GetType().GetProperty("ShowCancellationDetails")!.GetValue(m)! &&
                    !(bool)m.GetType().GetProperty("WasPaid")!.GetValue(m)!),
                Arg.Any<CancellationToken>())
            .Returns(expectedEmailBody);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == user.Email.Value &&
                m.Subject == "Booking Cancelled" &&
                m.Body == expectedEmailBody),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSendEmailToGuestWithDetails_WhenRefundProcessingAndPenaltyApplied()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, PaymentStatus.RefundProcessing);
        // Set refund amount to 800.00m (total is 1000.00m, penalty is 200.00m)
        var domainEvent = new BookingCancelledDomainEvent(booking.Id, RefundAmount: 800.00m, Currency: "USD");
        var apartment = CreateApartment();
        var transaction = CreateTransaction(booking.Id, "pi_test_intent");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(transaction);

        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        string expectedEmailBody = "<html>Email Content</html>";

        _emailTemplateServiceMock.GenerateEmailBodyAsync(
                "BookingCancelled.html",
                Arg.Is<object>(m =>
                    m.GetType().GetProperty("FirstName")!.GetValue(m)!.ToString() == user.FirstName.Value &&
                    m.GetType().GetProperty("ApartmentName")!.GetValue(m)!.ToString() == apartment.Name.Value &&
                    (decimal)m.GetType().GetProperty("RefundAmount")!.GetValue(m)! == 800.00m &&
                    (decimal)m.GetType().GetProperty("CancellationFee")!.GetValue(m)! == 200.00m &&
                    (bool)m.GetType().GetProperty("ShowCancellationDetails")!.GetValue(m)! &&
                    (bool)m.GetType().GetProperty("WasPaid")!.GetValue(m)!),
                Arg.Any<CancellationToken>())
            .Returns(expectedEmailBody);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == user.Email.Value &&
                m.Subject == "Booking Cancelled" &&
                m.Body == expectedEmailBody),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldEarlyReturnAndNotSendEmail_WhenBookingIsUnpaid()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, PaymentStatus.Unpaid);
        var domainEvent = new BookingCancelledDomainEvent(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock.DidNotReceiveWithAnyArgs().GenerateEmailBodyAsync(default!, default!);
        await _emailServiceMock.DidNotReceiveWithAnyArgs().SendAsync(default!);
        await _paymentGatewayMock.DidNotReceiveWithAnyArgs()
            .CancelPaymentIntentAsync(default!, Arg.Any<CancellationToken>());
        await _paymentGatewayMock.DidNotReceiveWithAnyArgs()
            .CreateRefundAsync(default!, default!, default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReleaseAuthorization_WhenPaymentStatusIsAuthorizationReleased()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, PaymentStatus.AuthorizationReleased);
        var domainEvent = new BookingCancelledDomainEvent(booking.Id);
        var transaction = CreateTransaction(booking.Id, "pi_test_intent");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(transaction);

        _paymentGatewayMock.CancelPaymentIntentAsync("pi_test_intent", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _paymentGatewayMock.Received(1).CancelPaymentIntentAsync("pi_test_intent", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldInitiateRefund_WhenPaymentStatusIsPaidAndRefundAmountGreaterThanZero()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, PaymentStatus.Paid);
        var domainEvent =
            new BookingCancelledDomainEvent(booking.Id, RefundAmount: 150.00m, Currency: "USD");
        var transaction = CreateTransaction(booking.Id, "pi_test_intent");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(transaction);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _paymentGatewayMock.Received(1)
            .CreateRefundAsync("pi_test_intent", 150.00m, "USD", Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        booking.PaymentStatus.Should().Be(PaymentStatus.RefundProcessing);
    }

    [Fact]
    public async Task Handle_ShouldSendEmailToHost_WhenCancelledByHostWithPenalty()
    {
        // Arrange
        var guest = CreateUser();
        var host = CreateUser();
        var booking = CreateBooking(guest.Id, PaymentStatus.Paid);
        var domainEvent = new BookingCancelledDomainEvent(
            booking.Id,
            RefundAmount: 200.00m,
            CancelledByHost: true,
            Currency: "USD",
            HostPenaltyAmount: 40.00m);

        var apartment = CreateApartment(host.Id);
        var transaction = CreateTransaction(booking.Id, "paid");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(transaction);

        _userRepositoryMock.GetByIdAsync(guest.Id, Arg.Any<CancellationToken>())
            .Returns(guest);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _userRepositoryMock.GetByIdAsync(host.Id, Arg.Any<CancellationToken>())
            .Returns(host);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock.Received(1).GenerateEmailBodyAsync(
            "BookingCancelledHost.html",
            Arg.Is<object>(m =>
                m.GetType().GetProperty("FirstName")!.GetValue(m)!.ToString() == host.FirstName.Value &&
                m.GetType().GetProperty("ApartmentName")!.GetValue(m)!.ToString() == apartment.Name.Value &&
                (decimal)m.GetType().GetProperty("PenaltyAmount")!.GetValue(m)! == 40.00m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSendEmailToHost_WhenCancelledByGuestWithPenalty()
    {
        // Arrange
        var guest = CreateUser();
        var host = CreateUser();
        var booking = CreateBooking(guest.Id, PaymentStatus.Paid);
        var domainEvent = new BookingCancelledDomainEvent(
            booking.Id,
            RefundAmount: 800.00m,
            CancelledByHost: false,
            Currency: "USD",
            HostPenaltyAmount: 0m); // guest penalty = TotalPrice (1000) - Refund (800) = 200

        var apartment = CreateApartment(host.Id);
        var transaction = CreateTransaction(booking.Id, "paid");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .Returns(transaction);

        _userRepositoryMock.GetByIdAsync(guest.Id, Arg.Any<CancellationToken>())
            .Returns(guest);

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _userRepositoryMock.GetByIdAsync(host.Id, Arg.Any<CancellationToken>())
            .Returns(host);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock.Received(1).GenerateEmailBodyAsync(
            "BookingCancelledHostByGuest.html",
            Arg.Is<object>(m =>
                m.GetType().GetProperty("FirstName")!.GetValue(m)!.ToString() == host.FirstName.Value &&
                m.GetType().GetProperty("ApartmentName")!.GetValue(m)!.ToString() == apartment.Name.Value &&
                (decimal)m.GetType().GetProperty("PenaltyAmount")!.GetValue(m)! == 200.00m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenTransactionNotFoundAndPaymentStatusIsPaidWithRefund()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, PaymentStatus.Paid);
        var domainEvent = new BookingCancelledDomainEvent(booking.Id, RefundAmount: 100.00m, Currency: "USD");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act & Assert
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Transaction not found for cancelled paid booking *");
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenTransactionNotFoundAndPaymentStatusIsAuthorizationReleased()
    {
        // Arrange
        var user = CreateUser();
        var booking = CreateBooking(user.Id, PaymentStatus.AuthorizationReleased);
        var domainEvent = new BookingCancelledDomainEvent(booking.Id);

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _transactionRepositoryMock.GetByBookingIdAsync(booking.Id, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act & Assert
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Transaction not found for cancelled booking *");
    }
}
