using Bookify.Domain.CancellationPolicies;
using Bookify.Domain.Bookings;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;
using Bookify.Domain.UnitTests.Apartments;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.CancellationPolicies;

public class CancellationPolicyEngineTests
{
    private readonly CancellationPolicyEngine _engine;
    private readonly CancellationPolicy _policy;
    private readonly Booking _booking;

    public CancellationPolicyEngineTests()
    {
        _engine = new CancellationPolicyEngine();

        // 1. Create default policy: 24h threshold
        // Early guest: 0%, Late guest: 50%
        // Early host: 10%, Late host: 100%
        _policy = CancellationPolicy.Create(
            "Standard Policy",
            earlyGuestPenaltyRate: 0.00m,
            lateGuestPenaltyRate: 0.50m,
            earlyHostPenaltyRate: 0.10m,
            lateHostPenaltyRate: 1.00m,
            thresholdHours: 24,
            isDefault: true,
            utcNow: DateTime.UtcNow);

        // 2. Create booking for USD 200 using proper domain factory methods
        var user = User.Create(
            new FirstName("Guest"),
            new LastName("User"),
            new Email("guest@test.com"),
            DateOfBirth.Create(new DateOnly(2000, 1, 1)),
            Role.Guest);

        var price = new Money(40.00m, Currency.Usd);
        var apartment = ApartmentData.Create(price, Money.Zero(Currency.Usd));
        var duration = DateRange.Create(new DateOnly(2025, 1, 10), new DateOnly(2025, 1, 15));
        var pricingService = new PricingService();

        _booking = Booking.Reserve(apartment, user.Id, duration, DateTime.UtcNow, pricingService);
    }

    [Fact]
    public void CalculatePenalty_ShouldReturnZeroPenaltyAndFullRefund_WhenGuestCancelsEarly()
    {
        // Arrange
        // Check-in is 2025-01-10 00:00:00. 48 hours before is 2025-01-08 00:00:00.
        DateTime utcNow = new(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc);

        // Act
        PenaltyResult result = _engine.CalculatePenalty(_booking, _policy, utcNow, cancelledByHost: false);

        // Assert
        result.GuestPenaltyAmount.Should().Be(0.00m);
        result.HostPenaltyAmount.Should().Be(0.00m);
        result.RefundAmount.Should().Be(200.00m);
        result.Currency.Should().Be("USD");
        result.RequiresRefund.Should().BeTrue();
    }

    [Fact]
    public void CalculatePenalty_ShouldReturnFiftyPercentPenalty_WhenGuestCancelsLate()
    {
        // Arrange
        // Check-in is 2025-01-10 00:00:00. 12 hours before is 2025-01-09 12:00:00.
        DateTime utcNow = new(2025, 1, 9, 12, 0, 0, DateTimeKind.Utc);

        // Act
        PenaltyResult result = _engine.CalculatePenalty(_booking, _policy, utcNow, cancelledByHost: false);

        // Assert
        result.GuestPenaltyAmount.Should().Be(100.00m); // 50% of 200
        result.HostPenaltyAmount.Should().Be(0.00m);
        result.RefundAmount.Should().Be(100.00m); // 200 - 100
        result.Currency.Should().Be("USD");
        result.RequiresRefund.Should().BeTrue();
    }

    [Fact]
    public void CalculatePenalty_ShouldReturnTenPercentHostPenaltyAndFullRefundToGuest_WhenHostCancelsEarly()
    {
        // Arrange
        // 48 hours before check-in
        DateTime utcNow = new(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc);

        // Act
        PenaltyResult result = _engine.CalculatePenalty(_booking, _policy, utcNow, cancelledByHost: true);

        // Assert
        result.GuestPenaltyAmount.Should().Be(0.00m);
        result.HostPenaltyAmount.Should().Be(20.00m); // 10% of 200
        result.RefundAmount.Should().Be(200.00m); // Capped at 100% of TotalPrice
        result.Currency.Should().Be("USD");
        result.RequiresRefund.Should().BeTrue();
    }

    [Fact]
    public void CalculatePenalty_ShouldReturnOneHundredPercentHostPenaltyAndFullRefundToGuest_WhenHostCancelsLate()
    {
        // Arrange
        // 12 hours before check-in
        DateTime utcNow = new(2025, 1, 9, 12, 0, 0, DateTimeKind.Utc);

        // Act
        PenaltyResult result = _engine.CalculatePenalty(_booking, _policy, utcNow, cancelledByHost: true);

        // Assert
        result.GuestPenaltyAmount.Should().Be(0.00m);
        result.HostPenaltyAmount.Should().Be(200.00m); // 100% of 200
        result.RefundAmount.Should().Be(200.00m); // Capped at 100% of TotalPrice
        result.Currency.Should().Be("USD");
        result.RequiresRefund.Should().BeTrue();
    }
}
