namespace Bookify.Domain.TaxRules;

public interface ITaxRuleRepository
{
    Task<IReadOnlyList<TaxRule>> GetActiveRulesForLocationAsync(
        string countryCode,
        string? region,
        string? city,
        DateOnly bookingDate,
        CancellationToken cancellationToken = default);

    Task<TaxRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(TaxRule taxRule);

    void Update(TaxRule taxRule);
}
