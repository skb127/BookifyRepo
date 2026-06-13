using Bookify.Domain.CancellationPolicies;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Infrastructure.Repositories;

internal sealed class CancellationPolicyRepository : Repository<CancellationPolicy>, ICancellationPolicyRepository
{
    public CancellationPolicyRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<CancellationPolicy?> GetDefaultAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Set<CancellationPolicy>()
            .FirstOrDefaultAsync(cp => cp.IsDefault, cancellationToken);
}
