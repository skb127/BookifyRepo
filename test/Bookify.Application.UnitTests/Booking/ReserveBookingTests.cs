using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Bookings.ReserveBooking;
using Bookify.Application.Exceptions;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Application.UnitTests.Users;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Booking;

public class ReserveBookingTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
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

    public ReserveBookingTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _pricingService = Substitute.For<PricingService>();
        _userContextMock = Substitute.For<IUserContext>();

        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new ReserveBookingCommandHandler(_userRepositoryMock,
            _apartmentRepositoryMock,
            _bookingRepositoryMock,
            _unitOfWorkMock,
            _pricingService,
            _dateTimeProviderMock,
            _userContextMock);
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
}