using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Bookings.ReserveBooking;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.TaxRules;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Bookify.Application.UnitTests.Booking;

public class TaxSnapshotServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private readonly TaxSnapshotService _service;
    private readonly ITaxRuleRepository _taxRuleRepositoryMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly ILogger<TaxSnapshotService> _loggerMock;

    public TaxSnapshotServiceTests()
    {
        _taxRuleRepositoryMock = Substitute.For<ITaxRuleRepository>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _loggerMock = Substitute.For<ILogger<TaxSnapshotService>>();

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _service = new TaxSnapshotService(
            _taxRuleRepositoryMock,
            _dateTimeProviderMock,
            _loggerMock);
    }

    [Fact]
    public async Task CalculateAndSnapshot_WithNoRules_ReturnsEmptyList()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService());

        _taxRuleRepositoryMock
            .GetActiveRulesForLocationAsync(
                apartment.Address.Country,
                apartment.Address.State,
                apartment.Address.City,
                DateOnly.FromDateTime(UtcNow),
                Arg.Any<CancellationToken>())
            .Returns(new List<TaxRule>());

        // Act
        IReadOnlyList<BookingTax> result = await _service.CalculateAndSnapshotAsync(
            booking,
            apartment,
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateAndSnapshot_WithPercentageRule_ReturnsCorrectAmount()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService()); // 4 nights * $50 = $200 total price

        var rule = TaxRule.Create(
            apartment.Address.Country,
            null,
            null,
            TaxRate.Percentage(0.10m), // 10% tax
            "VAT",
            new DateOnly(2020, 1, 1),
            null,
            UtcNow);

        _taxRuleRepositoryMock
            .GetActiveRulesForLocationAsync(
                apartment.Address.Country,
                apartment.Address.State,
                apartment.Address.City,
                DateOnly.FromDateTime(UtcNow),
                Arg.Any<CancellationToken>())
            .Returns(new List<TaxRule> { rule });

        // Act
        IReadOnlyList<BookingTax> result = await _service.CalculateAndSnapshotAsync(
            booking,
            apartment,
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        BookingTax snapshot = result[0];
        snapshot.BookingId.Should().Be(booking.Id);
        snapshot.TaxRuleName.Should().Be("VAT");
        snapshot.TaxType.Should().Be(TaxType.Percentage);
        snapshot.Rate.Should().Be(0.10m);
        snapshot.CalculatedAmount.Amount.Should().Be(booking.TotalPrice.Amount * 0.10m);
        snapshot.CalculatedAmount.Currency.Should().Be(booking.TotalPrice.Currency);
    }

    [Fact]
    public async Task CalculateAndSnapshot_WithPerNightRule_ReturnsCorrectAmount()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService()); // 4 nights

        var rule = TaxRule.Create(
            apartment.Address.Country,
            null,
            null,
            TaxRate.PerNight(5.00m), // $5 per night
            "Tourist Tax",
            new DateOnly(2020, 1, 1),
            null,
            UtcNow);

        _taxRuleRepositoryMock
            .GetActiveRulesForLocationAsync(
                apartment.Address.Country,
                apartment.Address.State,
                apartment.Address.City,
                DateOnly.FromDateTime(UtcNow),
                Arg.Any<CancellationToken>())
            .Returns(new List<TaxRule> { rule });

        // Act
        IReadOnlyList<BookingTax> result = await _service.CalculateAndSnapshotAsync(
            booking,
            apartment,
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        BookingTax snapshot = result[0];
        snapshot.BookingId.Should().Be(booking.Id);
        snapshot.TaxRuleName.Should().Be("Tourist Tax");
        snapshot.TaxType.Should().Be(TaxType.FixedPerNight);
        snapshot.Rate.Should().Be(5.00m);
        snapshot.CalculatedAmount.Amount.Should().Be(20.00m); // 4 nights * $5
        snapshot.CalculatedAmount.Currency.Should().Be(booking.TotalPrice.Currency);
    }

    [Fact]
    public async Task CalculateAndSnapshot_WithMultipleRules_ReturnsOneSnapshotPerRule()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)),
            UtcNow,
            new PricingService()); // 4 nights * $50 = $200 total price

        var rule1 = TaxRule.Create(
            apartment.Address.Country,
            null,
            null,
            TaxRate.Percentage(0.10m),
            "VAT",
            new DateOnly(2020, 1, 1),
            null,
            UtcNow);

        var rule2 = TaxRule.Create(
            apartment.Address.Country,
            null,
            null,
            TaxRate.PerNight(3.00m),
            "Local Tax",
            new DateOnly(2020, 1, 1),
            null,
            UtcNow);

        _taxRuleRepositoryMock
            .GetActiveRulesForLocationAsync(
                apartment.Address.Country,
                apartment.Address.State,
                apartment.Address.City,
                DateOnly.FromDateTime(UtcNow),
                Arg.Any<CancellationToken>())
            .Returns(new List<TaxRule> { rule1, rule2 });

        // Act
        IReadOnlyList<BookingTax> result = await _service.CalculateAndSnapshotAsync(
            booking,
            apartment,
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainSingle(t => t.TaxRuleName == "VAT" && t.CalculatedAmount.Amount == 20.00m);
        result.Should().ContainSingle(t => t.TaxRuleName == "Local Tax" && t.CalculatedAmount.Amount == 12.00m);
    }

    [Fact]
    public async Task CalculateAndSnapshot_SnapshotBookingId_MatchesBooking()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 3)),
            UtcNow,
            new PricingService());

        var rule = TaxRule.Create(
            apartment.Address.Country,
            null,
            null,
            TaxRate.PerBooking(15.00m),
            "Flat Tax",
            new DateOnly(2020, 1, 1),
            null,
            UtcNow);

        _taxRuleRepositoryMock
            .GetActiveRulesForLocationAsync(
                apartment.Address.Country,
                apartment.Address.State,
                apartment.Address.City,
                DateOnly.FromDateTime(UtcNow),
                Arg.Any<CancellationToken>())
            .Returns(new List<TaxRule> { rule });

        // Act
        IReadOnlyList<BookingTax> result = await _service.CalculateAndSnapshotAsync(
            booking,
            apartment,
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public async Task CalculateAndSnapshot_WithPerPersonPerNightRule_UsesBookingGuestCount()
    {
        // Arrange
        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)), // 4 nights
            UtcNow,
            new PricingService(),
            guestCount: 3);

        var rule = TaxRule.Create(
            apartment.Address.Country,
            null,
            null,
            TaxRate.PerPersonPerNight(2.00m), // $2 per person per night
            "Tourist Tax",
            new DateOnly(2020, 1, 1),
            null,
            UtcNow);

        _taxRuleRepositoryMock
            .GetActiveRulesForLocationAsync(
                apartment.Address.Country,
                apartment.Address.State,
                apartment.Address.City,
                DateOnly.FromDateTime(UtcNow),
                Arg.Any<CancellationToken>())
            .Returns(new List<TaxRule> { rule });

        // Act
        IReadOnlyList<BookingTax> result = await _service.CalculateAndSnapshotAsync(
            booking,
            apartment,
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        BookingTax snapshot = result[0];
        // 4 nights * 3 guests * $2 = $24
        snapshot.CalculatedAmount.Amount.Should().Be(24.00m);
    }
}
