using Bookify.Application.Abstractions.Clock;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Shared;
using Bookify.Domain.TaxRules;
using Microsoft.Extensions.Logging;

namespace Bookify.Application.Bookings.ReserveBooking;

internal sealed class TaxSnapshotService : ITaxSnapshotService
{
    private readonly ITaxRuleRepository _taxRuleRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<TaxSnapshotService> _logger;

    public TaxSnapshotService(
        ITaxRuleRepository taxRuleRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<TaxSnapshotService> logger)
    {
        _taxRuleRepository = taxRuleRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BookingTax>> CalculateAndSnapshotAsync(
        Booking booking,
        Apartment apartment,
        CancellationToken cancellationToken)
    {
        int nights = booking.Duration.LengthInDays;
        var bookingDate = DateOnly.FromDateTime(_dateTimeProvider.UtcNow);

        IReadOnlyList<TaxRule> rules = await _taxRuleRepository
            .GetActiveRulesForLocationAsync(
                apartment.Address.Country,
                apartment.Address.State,
                apartment.Address.City,
                bookingDate,
                cancellationToken);

        if (rules.Count == 0)
        {
            _logger.LogWarning("No active tax rules found for location {Country}/{State}/{City}",
                apartment.Address.Country, apartment.Address.State, apartment.Address.City);
            return [];
        }

        return rules.Select(rule =>
        {
            Money taxAmount = rule.CalculateTaxAmount(
                booking.TotalPrice,
                nights,
                guests: 1); // Note: Hardcoded to 1 guest; will be updated when GuestCount is introduced in a future phase.

            return BookingTax.CreateSnapshot(
                Guid.CreateVersion7(),
                booking.Id,
                rule,
                taxAmount,
                _dateTimeProvider.UtcNow);
        }).ToList().AsReadOnly();
    }
}
