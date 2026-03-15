using Bookify.Application.Apartments.CheckApartmentAvailability;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Apartments;

public class CheckApartmentAvailabilityTests
{
    private static readonly CheckApartmentAvailabilityQuery Query = new(
        Guid.CreateVersion7(),
        new DateOnly(2026, 3, 1),
        new DateOnly(2026, 3, 10));

    private readonly CheckApartmentAvailabilityQueryHandler _handler;

    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly IBookingRepository _bookingRepositoryMock;

    public CheckApartmentAvailabilityTests()
    {
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();

        _handler = new CheckApartmentAvailabilityQueryHandler(
            _apartmentRepositoryMock,
            _bookingRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenApartmentNotFound()
    {
        // Arrange
        _apartmentRepositoryMock
            .GetByIdAsync(Query.ApartmentId, Arg.Any<CancellationToken>())
            .Returns((Apartment?)null);

        // Act
        Result<ApartmentAvailabilityResponse> result = await _handler.Handle(Query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApartmentErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnIsAvailableTrue_WhenNoOverlap()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var duration = DateRange.Create(Query.StartDate, Query.EndDate);

        _apartmentRepositoryMock
            .GetByIdAsync(Query.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _bookingRepositoryMock
            .IsOverlappingAsync(apartment, duration, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<ApartmentAvailabilityResponse> result = await _handler.Handle(Query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnIsAvailableFalse_WhenOverlappingExists()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var duration = DateRange.Create(Query.StartDate, Query.EndDate);

        _apartmentRepositoryMock
            .GetByIdAsync(Query.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _bookingRepositoryMock
            .IsOverlappingAsync(apartment, duration, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result<ApartmentAvailabilityResponse> result = await _handler.Handle(Query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnCorrectDates_WhenQuerySucceeds()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var duration = DateRange.Create(Query.StartDate, Query.EndDate);

        _apartmentRepositoryMock
            .GetByIdAsync(Query.ApartmentId, Arg.Any<CancellationToken>())
            .Returns(apartment);

        _bookingRepositoryMock
            .IsOverlappingAsync(apartment, duration, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<ApartmentAvailabilityResponse> result = await _handler.Handle(Query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StartDate.Should().Be(Query.StartDate);
        result.Value.EndDate.Should().Be(Query.EndDate);
    }
}
