using Bookify.Domain.TaxRules;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Infrastructure.Repositories;

internal sealed class TaxRuleRepository : Repository<TaxRule>, ITaxRuleRepository
{
    public TaxRuleRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<TaxRule>> GetActiveRulesForLocationAsync(
        string countryCode,
        string? region,
        string? city,
        DateOnly bookingDate,
        CancellationToken cancellationToken = default) =>
        await DbContext.Set<TaxRule>()
            .Where(tr => tr.CountryCode == countryCode &&
                         tr.IsActive &&
                         (tr.Region == null || tr.Region == region) &&
                         (tr.City == null || tr.City == city) &&
                         tr.EffectiveFrom <= bookingDate &&
                         (tr.EffectiveTo == null || tr.EffectiveTo >= bookingDate))
            .ToListAsync(cancellationToken);
}
