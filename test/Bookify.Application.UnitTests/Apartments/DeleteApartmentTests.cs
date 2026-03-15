using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Apartments.DeleteApartment;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Apartments;

public class DeleteApartmentTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    private static readonly Guid ApartmentId = Guid.NewGuid();

    private static readonly DeleteApartmentCommand Command = new(ApartmentId);

    private readonly DeleteApartmentCommandHandler _handler;

    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;

    public DeleteApartmentTests()
    {
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new DeleteApartmentCommandHandler(
            _apartmentRepositoryMock,
            _bookingRepositoryMock,
            _unitOfWorkMock,
            _dateTimeProviderMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenApartmentNotFound()
    {
        // Arrange
        _apartmentRepositoryMock
            .GetByIdAsync(ApartmentId, Arg.Any<CancellationToken>())
            .Returns((Apartment?)null);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApartmentErrors.NotFound);

        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenApartmentHasActiveBookings()
    {
        // Arrange
        _apartmentRepositoryMock
            .GetByIdAsync(ApartmentId, Arg.Any<CancellationToken>())
            .Returns(ApartmentData.Create());

        _bookingRepositoryMock
            .HasActiveBookingsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApartmentErrors.HasActiveBookings);

        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndCallSaveChanges_WhenApartmentCanBeDeleted()
    {
        // Arrange
        _apartmentRepositoryMock
            .GetByIdAsync(ApartmentId, Arg.Any<CancellationToken>())
            .Returns(ApartmentData.Create());

        _bookingRepositoryMock
            .HasActiveBookingsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSetDeletedAt_WhenApartmentCanBeDeleted()
    {
        // Arrange
        var apartment = ApartmentData.Create();

        _apartmentRepositoryMock
            .GetByIdAsync(ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _bookingRepositoryMock
            .HasActiveBookingsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _handler.Handle(Command, CancellationToken.None);

        // Assert
        apartment.DeletedAt.Should().NotBeNull();
    }
}
