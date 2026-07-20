using System.Collections.Concurrent;
using Bookify.Application.Abstractions.Payments;

namespace Bookify.Application.IntegrationTests.Infrastructure;

public class MockStripeCustomerService : IStripeCustomerService
{
    public ConcurrentQueue<(Guid UserId, string Email, string Name)> UpsertCalls { get; } = new();
    public ConcurrentQueue<(string CustomerId, string Email, string Name)> UpdateCalls { get; } = new();
    public ConcurrentQueue<string> DeactivateCalls { get; } = new();

    public Task<string> UpsertCustomerAsync(
        Guid userId,
        string email,
        string name,
        CancellationToken cancellationToken = default)
    {
        UpsertCalls.Enqueue((userId, email, name));
        return Task.FromResult($"cus_mock_{userId:N}");
    }

    public Task UpdateCustomerAsync(
        string stripeCustomerId,
        string email,
        string name,
        CancellationToken cancellationToken = default)
    {
        UpdateCalls.Enqueue((stripeCustomerId, email, name));
        return Task.CompletedTask;
    }

    public Task DeactivateCustomerAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken = default)
    {
        DeactivateCalls.Enqueue(stripeCustomerId);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        UpsertCalls.Clear();
        UpdateCalls.Clear();
        DeactivateCalls.Clear();
    }
}
