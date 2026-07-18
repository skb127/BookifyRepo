using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Bookings.ReserveBooking;
using Bookify.Application.Exceptions;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Application.UnitTests.Users;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Booking;

public class ReserveBookingCommandHandlerTests
{
    private static readonly DateTime UtcNow = new (2025, 12, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ReserveBookingCommand Command = new(
        Guid.CreateVersion7(),
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 1, 10));

    private readonly ReserveBookingCommandHandler _handler; // SUT: System Under Test

    private readonly IUserRepository _userRepositoryMock;
    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly PricingService _pricingService;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUserContext _userContextMock;
    private readonly ITaxSnapshotService _taxSnapshotServiceMock;

    public ReserveBookingCommandHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _pricingService = Substitute.For<PricingService>();
        _userContextMock = Substitute.For<IUserContext>();
        _taxSnapshotServiceMock = Substitute.For<ITaxSnapshotService>();

        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _taxSnapshotServiceMock
            .CalculateAndSnapshotAsync(Arg.Any<Domain.Bookings.Booking>(), Arg.Any<Apartment>(), Arg.Any<CancellationToken>())
            .Returns(new List<BookingTax>().AsReadOnly());

        _handler = new ReserveBookingCommandHandler(_userRepositoryMock,
            _apartmentRepositoryMock,
            _bookingRepositoryMock,
            _unitOfWorkMock,
            _pricingService,
            _dateTimeProviderMock,
            _userContextMock,
            _taxSnapshotServiceMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTheUserIsNull()
    {
        // Arrange
        _userContextMock.UserId.Returns(Guid.CreateVersion7());
        _userRepositoryMock
            .GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act 
        Result<Guid> result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(UserErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTheApartmentIsNull()
    {
        // Arrange
        var user = UserData.Create();
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock
            .GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _apartmentRepositoryMock
            .GetByIdAsync(Command.ApartmentId, CancellationToken.None)
            .Returns((Apartment?)null);

        // Act 
        Result<Guid> result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(ApartmentErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTheApartmentBookingIsOverlapping()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var duration = DateRange.Create(Command.StartDate, Command.EndDate);

        var user = UserData.Create();
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock
            .GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _apartmentRepositoryMock
            .GetByIdAsync(Command.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _bookingRepositoryMock
            .IsOverlappingAsync(apartment, duration, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act 
        Result<Guid> result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(BookingErrors.Overlap);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUnitOfWorkThrows()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var duration = DateRange.Create(Command.StartDate, Command.EndDate);

        var user = UserData.Create();
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock
            .GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _apartmentRepositoryMock
            .GetByIdAsync(Command.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _bookingRepositoryMock
            .IsOverlappingAsync(apartment, duration, Arg.Any<CancellationToken>())
            .Returns(false);

        _unitOfWorkMock
            .SaveChangesAsync()
            .ThrowsAsync(new ConcurrencyException("Concurrency", new InvalidOperationException()));

        // Act 
        Result<Guid> result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(BookingErrors.Overlap);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenBookingIsReserved()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var duration = DateRange.Create(Command.StartDate, Command.EndDate);

        var user = UserData.Create();
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock
            .GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _apartmentRepositoryMock
            .GetByIdAsync(Command.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _bookingRepositoryMock
            .IsOverlappingAsync(apartment, duration, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act 
        Result<Guid> result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldCallRepository_WhenBookingIsReserved()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var duration = DateRange.Create(Command.StartDate, Command.EndDate);

        var user = UserData.Create();
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock
            .GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _apartmentRepositoryMock
            .GetByIdAsync(Command.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _bookingRepositoryMock
            .IsOverlappingAsync(apartment, duration, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act 
        Result<Guid> result = await _handler.Handle(Command, CancellationToken.None);

        // Assert, verify that the booking repository mock was called with the expected parameters
        _bookingRepositoryMock
            .Received(1)
            .Add(Arg.Is<Domain.Bookings.Booking>(b => b.Id == result.Value));
    }

    [Fact]
    public async Task Handle_ShouldCallUnitOfWork_WhenBookingIsReserved()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var duration = DateRange.Create(Command.StartDate, Command.EndDate);

        var user = UserData.Create();
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock
            .GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _apartmentRepositoryMock
            .GetByIdAsync(Command.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _bookingRepositoryMock
            .IsOverlappingAsync(apartment, duration, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act 
        Result<Guid> result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateBookingInPendingPaymentState_WhenInstantBookingIsTrue()
    {
        // Arrange
        var apartment = ApartmentData.CreateWithInstantBooking();

        var duration = DateRange.Create(Command.StartDate, Command.EndDate);

        var user = UserData.Create();
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock
            .GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _apartmentRepositoryMock
            .GetByIdAsync(Command.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _bookingRepositoryMock
            .IsOverlappingAsync(apartment, duration, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act 
        Result<Guid> result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _bookingRepositoryMock
            .Received(1)
            .Add(Arg.Is<Domain.Bookings.Booking>(b => b.Id == result.Value && b.Status == BookingStatus.PendingPayment));
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenDurationIsLessThanMinimumNights()
    {
        // Arrange
        var apartment = new Apartment(Guid.CreateVersion7(), Guid.NewGuid(), new Name("Apartment 1"), new Description("Apartment 1 description"), new Address("Country", "State", "ZipCode", "City", "Street"), new Money(50.0m, Currency.Usd), Money.Zero(), [], DateTime.UtcNow, false, null, 10, 3);
        
        var user = UserData.Create();
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>()).Returns(user);

        _apartmentRepositoryMock.GetByIdAsync(Command.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);

        // Act 
        Result<Guid> result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(BookingErrors.BelowMinimumNights);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCheckInIsBeforeCutOff()
    {
        // Arrange
        // Apartment requires 48 hours cut-off
        var apartment = new Apartment(Guid.CreateVersion7(), Guid.NewGuid(), new Name("Apartment 1"), new Description("Apartment 1 description"), new Address("Country", "State", "ZipCode", "City", "Street"), new Money(50.0m, Currency.Usd), Money.Zero(), [], DateTime.UtcNow, false, null, 1, 48);

        // Command wants to check in on 2026-01-01
        // UtcNow is 2025-12-31 00:00:00 (which is only 24 hours before 2026-01-01)
        _dateTimeProviderMock.UtcNow.Returns(new DateTime(2025, 12, 31, 12, 0, 0, DateTimeKind.Utc));

        var user = UserData.Create();
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>()).Returns(user);

        _apartmentRepositoryMock.GetByIdAsync(Command.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);

        // Act 
        Result<Guid> result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(BookingErrors.CheckInTooSoon);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenGuestCountExceedsMaxGuests()
    {
        // Arrange
        var apartment = ApartmentData.Create(baseGuests: 1, maxGuests: 2);
        var user = UserData.Create();
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>()).Returns(user);
        _apartmentRepositoryMock.GetByIdAsync(Command.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);

        var commandWithManyGuests = Command with { GuestCount = 3 };

        // Act
        Result<Guid> result = await _handler.Handle(commandWithManyGuests, CancellationToken.None);

        // Assert
        result.Error.Should().Be(BookingErrors.ExceedsMaxGuests);
    }

    [Fact]
    public async Task Handle_ShouldCreateBooking_WithCorrectGuestCount()
    {
        // Arrange
        var apartment = ApartmentData.Create(baseGuests: 1, maxGuests: 4);
        var user = UserData.Create();
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdAsync(_userContextMock.UserId, Arg.Any<CancellationToken>()).Returns(user);
        _apartmentRepositoryMock.GetByIdAsync(Command.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);


        var commandWithGuests = Command with { GuestCount = 3 };

        // Act
        Result<Guid> result = await _handler.Handle(commandWithGuests, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _bookingRepositoryMock.Received(1).Add(Arg.Is<Domain.Bookings.Booking>(b => b.GuestCount == 3));
    }
}