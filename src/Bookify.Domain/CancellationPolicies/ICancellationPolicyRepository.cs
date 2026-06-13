namespace Bookify.Domain.CancellationPolicies;

public interface ICancellationPolicyRepository
{
    Task<CancellationPolicy?> GetDefaultAsync(CancellationToken cancellationToken = default);

    Task<CancellationPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(CancellationPolicy cancellationPolicy);
}
