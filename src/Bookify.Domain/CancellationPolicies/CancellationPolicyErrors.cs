using Bookify.Domain.Abstractions;

namespace Bookify.Domain.CancellationPolicies;

public static class CancellationPolicyErrors
{
    public static Error NotFound { get; } = new(
        "CancellationPolicy.NotFound",
        "The cancellation policy with the specified identifier was not found");
}
