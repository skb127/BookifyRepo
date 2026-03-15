using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Authorization;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Bookings.RejectBooking;
using Bookify.Application.Exceptions;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking;

public class RejectBookingTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;

    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IUserContext _userContextMock;
    private readonly IAuthorizationService _authorizationServiceMock;
    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly RejectBookingCommandHandler _handler;

    public RejectBookingTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _userContextMock = Substitute.For<IUserContext>();
        _authorizationServiceMock = Substitute.For<IAuthorizationService>();
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();

        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);
        
        _handler = new RejectBookingCommandHandler(
            _dateTimeProviderMock,
            _bookingRepositoryMock,
            _unitOfWorkMock,
            _userContextMock,
            _authorizationServiceMock,
            _apartmentRepositoryMock);
    }

    private static Domain.Bookings.Booking CreateBooking(BookingStatus status, Guid apartmentId)
    {
        var booking = (Domain.Bookings.Booking)Activator.CreateInstance(typeof(Domain.Bookings.Booking), true)!;
        typeof(Domain.Bookings.Booking).GetProperty(nameof(Domain.Bookings.Booking.Status))!.SetValue(booking, status);
        typeof(Domain.Bookings.Booking).GetProperty(nameof(Domain.Bookings.Booking.ApartmentId))!.SetValue(booking, apartmentId);
        return booking;
    }

    private static Apartment CreateApartment(Guid ownerId)
    {
        var apartment = (Apartment)Activator.CreateInstance(typeof(Apartment), true)!;
        typeof(Apartment).GetProperty(nameof(Apartment.OwnerId))!.SetValue(apartment, ownerId);
        return apartment;
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingNotFound()
    {
        // Arrange
        var command = new RejectBookingCommand(Guid.NewGuid());

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsNotAdminAndApartmentNotFound()
    {
        // Arrange
        var command = new RejectBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Reserved, Guid.NewGuid());
        string identityId = "test-identity";

        _userContextMock.IdentityId.Returns(identityId);
        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _authorizationServiceMock.GetPermissionsForUserAsync(identityId).Returns(new HashSet<string>());

        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApartmentErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsNotAdminAndNotOwner()
    {
        // Arrange
        var command = new RejectBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Reserved, Guid.NewGuid());
        var apartment = CreateApartment(Guid.NewGuid()); // Owner is different
        string identityId = "test-identity";
        Guid userId = Guid.NewGuid();

        _userContextMock.IdentityId.Returns(identityId);
        _userContextMock.UserId.Returns(userId);

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _authorizationServiceMock.GetPermissionsForUserAsync(identityId).Returns(new HashSet<string>());
        _apartmentRepositoryMock.GetByIdAsync(booking.ApartmentId, Arg.Any<CancellationToken>()).Returns(apartment);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.Unauthorized);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingIsNotReserved()
    {
        // Arrange
        var command = new RejectBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Confirmed, Guid.NewGuid()); // Not Reserved
        string identityId = "admin-identity";

        _userContextMock.IdentityId.Returns(identityId);
        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>()).Returns(booking);

        // Setup user as Admin
        _authorizationServiceMock.GetPermissionsForUserAsync(identityId)
            .Returns(new HashSet<string> { Permission.BookingsWrite.Name });
        
        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotReserved);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenUserIsAdmin()
    {
        // Arrange
        var command = new RejectBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Reserved, Guid.NewGuid());
        string identityId = "admin-identity";

        _userContextMock.IdentityId.Returns(identityId);
        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>()).Returns(booking);

        // Setup user as Admin
        _authorizationServiceMock.GetPermissionsForUserAsync(identityId)
            .Returns(new HashSet<string> { Permission.BookingsWrite.Name });

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenUserIsApartmentOwner()
    {
        // Arrange
        var command = new RejectBookingCommand(Guid.NewGuid());
        var apartmentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var booking = CreateBooking(BookingStatus.Reserved, apartmentId);
        var apartment = CreateApartment(ownerId);
        string identityId = "owner-identity";

        _userContextMock.IdentityId.Returns(identityId);
        _userContextMock.UserId.Returns(ownerId); // Current user is owner

        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>()).Returns(booking);
        _authorizationServiceMock.GetPermissionsForUserAsync(identityId).Returns(new HashSet<string>()); // No admin permission
        _apartmentRepositoryMock.GetByIdAsync(apartmentId, Arg.Any<CancellationToken>()).Returns(apartment);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUnitOfWorkThrows()
    {
        // Arrange
        var command = new RejectBookingCommand(Guid.NewGuid());
        var booking = CreateBooking(BookingStatus.Reserved, Guid.NewGuid());
        string identityId = "admin-identity";

        _userContextMock.IdentityId.Returns(identityId);
        _bookingRepositoryMock.GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>()).Returns(booking);

        // Setup user as Admin
        _authorizationServiceMock.GetPermissionsForUserAsync(identityId)
            .Returns([Permission.BookingsWrite.Name]);

        _unitOfWorkMock.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyException("Concurrency", new InvalidOperationException()));

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>();
    }
}
