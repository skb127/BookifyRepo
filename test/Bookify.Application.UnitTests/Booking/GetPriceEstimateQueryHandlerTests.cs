using Bookify.Application.Bookings.GetPriceEstimate;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Shared;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking;

public class GetPriceEstimateQueryHandlerTests
{
    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly PricingService _pricingService;
    private readonly GetPriceEstimateQueryHandler _handler;

    public GetPriceEstimateQueryHandlerTests()
    {
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _pricingService = new PricingService(); // Domain service without dependencies

        _handler = new GetPriceEstimateQueryHandler(
            _apartmentRepositoryMock,
            _pricingService);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenApartmentNotFound()
    {
        // Arrange
        var query = new GetPriceEstimateQuery(Guid.NewGuid(), new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 5));

        _apartmentRepositoryMock.GetByIdAsync(query.ApartmentId, CancellationToken.None)
            .ReturnsNull();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApartmentErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithPricingDetails_WhenApartmentExists()
    {
        // Arrange
        var query = new GetPriceEstimateQuery(Guid.NewGuid(), new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 5)); // 4 days

        // Construct fake apartment
        var price = new Money(100.0m, Currency.Usd);
        var cleaningFee = new Money(50.0m, Currency.Usd);
        var apartment = (Bookify.Domain.Apartments.Apartment)Activator.CreateInstance(typeof(Bookify.Domain.Apartments.Apartment), true)!;
        typeof(Bookify.Domain.Apartments.Apartment).GetProperty(nameof(Bookify.Domain.Apartments.Apartment.Price))!.SetValue(apartment, price);
        typeof(Bookify.Domain.Apartments.Apartment).GetProperty(nameof(Bookify.Domain.Apartments.Apartment.CleaningFee))!.SetValue(apartment, cleaningFee);
        typeof(Bookify.Domain.Apartments.Apartment).GetProperty(nameof(Bookify.Domain.Apartments.Apartment.Amenities))!.SetValue(apartment, new List<Amenity>()); // 0% upcharge

        _apartmentRepositoryMock.GetByIdAsync(query.ApartmentId, CancellationToken.None)
            .Returns(apartment);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 4 days * $100 = $400
        result.Value.PriceForPeriodAmount.Should().Be(400.0m);
        result.Value.PriceForPeriodCurrency.Should().Be("USD");

        // $50 cleaning fee
        result.Value.CleaningFeeAmount.Should().Be(50.0m);
        result.Value.CleaningFeeCurrency.Should().Be("USD");

        // 0 upcharge
        result.Value.AmenitiesUpChargeAmount.Should().Be(0m);

        // Total = 450
        result.Value.TotalAmount.Should().Be(450.0m);
        result.Value.TotalCurrency.Should().Be("USD");

        result.Value.LengthInDays.Should().Be(4);
    }
}
