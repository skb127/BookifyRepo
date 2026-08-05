using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Application.Bookings.Events;
using Bookify.Application.Options;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking.Events;

public class BookingPaymentAuthorizedDomainEventHandlerTests
{
    private static readonly DateTime UtcNow = new (2024, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUserRepository _userRepositoryMock;
    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly IEmailService _emailServiceMock;
    private readonly IEmailTemplateService _emailTemplateServiceMock;
    private readonly IJobScheduler _jobSchedulerMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IOptions<BookifyAppOptions> _appOptionsMock;
    private readonly IOptions<BookingOptions> _bookingOptionsMock;

    private readonly BookingPaymentAuthorizedDomainEventHandler _handler;

    public BookingPaymentAuthorizedDomainEventHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _emailServiceMock = Substitute.For<IEmailService>();
        _emailTemplateServiceMock = Substitute.For<IEmailTemplateService>();
        _jobSchedulerMock = Substitute.For<IJobScheduler>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _appOptionsMock = Substitute.For<IOptions<BookifyAppOptions>>();
        _bookingOptionsMock = Substitute.For<IOptions<BookingOptions>>();

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _appOptionsMock.Value.Returns(new BookifyAppOptions
        {
            FrontendUrl = new Uri("https://test.bookify.com")
        });

        _bookingOptionsMock.Value.Returns(new BookingOptions
        {
            CheckoutSessionTtlMinutes = 30.0,
            HostApprovalTtlHours = 24.0
        });

        _handler = new BookingPaymentAuthorizedDomainEventHandler(
            _bookingRepositoryMock,
            _userRepositoryMock,
            _apartmentRepositoryMock,
            _emailServiceMock,
            _emailTemplateServiceMock,
            _jobSchedulerMock,
            _dateTimeProviderMock,
            _unitOfWorkMock,
            _appOptionsMock,
            _bookingOptionsMock);
    }

    private static User CreateUser()
    {
        var firstName = new FirstName("Test");
        var lastName = new LastName("User");
        var email = new Email("test@test.com");
        var dateOfBirth = DateOfBirth.Create(new DateOnly(2000, 1, 1));

        return User.Create(firstName, lastName, email, dateOfBirth, Role.Guest);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_OrScheduleJobs_WhenBookingNotFound()
    {
        // Arrange
        var domainEvent = new BookingPaymentAuthorizedDomainEvent(Guid.NewGuid(), "session-id", "intent-id");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _jobSchedulerMock.DidNotReceiveWithAnyArgs().CancelExpireCheckoutSessionAsync(Guid.Empty);
        await _jobSchedulerMock.DidNotReceiveWithAnyArgs().ScheduleExpireHostApprovalAsync(Guid.Empty, default);
        await _unitOfWorkMock.DidNotReceiveWithAnyArgs().SaveChangesAsync();
        await _emailTemplateServiceMock.DidNotReceiveWithAnyArgs()
            .GenerateEmailBodyAsync(null!, null!, CancellationToken.None);
        await _emailServiceMock.DidNotReceiveWithAnyArgs().SendAsync(null!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenUserNotFound()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)),
            UtcNow,
            new PricingService());

        var domainEvent = new BookingPaymentAuthorizedDomainEvent(booking.Id, "session-id", "intent-id");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userRepositoryMock.GetByIdAsync(booking.UserId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _jobSchedulerMock.Received(1).CancelExpireCheckoutSessionAsync(domainEvent.BookingId, Arg.Any<CancellationToken>());
        await _jobSchedulerMock.Received(1).ScheduleExpireHostApprovalAsync(domainEvent.BookingId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await _emailTemplateServiceMock.DidNotReceiveWithAnyArgs()
            .GenerateEmailBodyAsync(null!, null!, CancellationToken.None);
        await _emailServiceMock.DidNotReceiveWithAnyArgs().SendAsync(null!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldCancelTtl1_ScheduleTtl2_AndSendEmail_WhenValid()
    {
        // Arrange
        var user = CreateUser();
        var host = User.Create(new FirstName("Host"), new LastName("User"), new Email("host@test.com"), DateOfBirth.Create(new DateOnly(1990, 1, 1)), Role.Host);

        Apartment apartment = ApartmentData.Create();
        typeof(Apartment).GetProperty("OwnerId")!.SetValue(apartment, host.Id);
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            user.Id,
            DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)),
            UtcNow,
            new PricingService());

        var domainEvent = new BookingPaymentAuthorizedDomainEvent(booking.Id, "session-id", "intent-id");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
            
        _userRepositoryMock.GetByIdAsync(host.Id, Arg.Any<CancellationToken>())
            .Returns(host);

        _apartmentRepositoryMock.GetByIdAsync(apartment.Id, Arg.Any<CancellationToken>())
            .Returns(apartment);

        string expectedEmailBody = "<html>Email Content</html>";
        string expectedHostEmailBody = "<html>Host Content</html>";

        _emailTemplateServiceMock.GenerateEmailBodyAsync(
                "BookingPaymentAuthorized.html",
                Arg.Any<object>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedEmailBody);
            
        _emailTemplateServiceMock.GenerateEmailBodyAsync(
                "BookingApprovalRequired.html",
                Arg.Any<object>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedHostEmailBody);

        var expectedExpiresAt = UtcNow.AddHours(24.0);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        booking.ExpiresAt.Should().BeCloseTo(expectedExpiresAt, TimeSpan.FromMilliseconds(100));

        await _jobSchedulerMock.Received(1).CancelExpireCheckoutSessionAsync(domainEvent.BookingId, Arg.Any<CancellationToken>());
        await _jobSchedulerMock.Received(1).ScheduleExpireHostApprovalAsync(
            domainEvent.BookingId,
            Arg.Is<DateTime>(dt => Math.Abs((dt - expectedExpiresAt).TotalMilliseconds) < 100),
            Arg.Any<CancellationToken>());

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await _emailTemplateServiceMock.Received(1).GenerateEmailBodyAsync(
            "BookingPaymentAuthorized.html",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
            
        await _emailTemplateServiceMock.Received(1).GenerateEmailBodyAsync(
            "BookingApprovalRequired.html",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());

        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == user.Email.Value &&
                m.Subject == "Booking Payment Authorized" &&
                m.Body == expectedEmailBody),
            Arg.Any<CancellationToken>());
            
        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == host.Email.Value &&
                m.Subject == "Booking Approval Required" &&
                m.Body == expectedHostEmailBody),
            Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task Handle_ShouldCapTtl2_WhenBookingIsCloseToCheckIn()
    {
        // Arrange
        var user = CreateUser();
        var host = User.Create(new FirstName("Host"), new LastName("User"), new Email("host@test.com"), DateOfBirth.Create(new DateOnly(1990, 1, 1)), Role.Host);

        Apartment apartment = ApartmentData.Create();
        typeof(Apartment).GetProperty("OwnerId")!.SetValue(apartment, host.Id);
        var checkInDate = new DateOnly(2025, 1, 1);
        
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            user.Id,
            DateRange.Create(checkInDate, new DateOnly(2025, 1, 10)),
            UtcNow,
            new PricingService());

        var domainEvent = new BookingPaymentAuthorizedDomainEvent(booking.Id, "session-id", "intent-id");

        _bookingRepositoryMock.GetByIdAsync(domainEvent.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _userRepositoryMock.GetByIdAsync(host.Id, Arg.Any<CancellationToken>()).Returns(host);
        _apartmentRepositoryMock.GetByIdAsync(apartment.Id, Arg.Any<CancellationToken>()).Returns(apartment);

        // Act: UtcNow is 2024-12-31 10:00:00 (less than 24h away from end of CheckInDate 2025-01-01 23:59:59)
        _dateTimeProviderMock.UtcNow.Returns(new DateTime(2024, 12, 31, 10, 0, 0, DateTimeKind.Utc));
        // checkInDate end of day is 2025-01-01 23:59:59. If UtcNow + 24 hours is 2025-01-01 10:00:00, that is NOT capped.
        // We need UtcNow to be 2025-01-01 10:00:00 so UtcNow + 24 hours is 2025-01-02 10:00:00, which is > 2025-01-01 23:59:59.
        var currentDate = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        _dateTimeProviderMock.UtcNow.Returns(currentDate);

        var expectedExpiresAt = checkInDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        booking.ExpiresAt.Should().BeCloseTo(expectedExpiresAt, TimeSpan.FromMilliseconds(100));
    }
}
