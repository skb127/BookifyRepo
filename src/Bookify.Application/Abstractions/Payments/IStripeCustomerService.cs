namespace Bookify.Application.Abstractions.Payments;

public interface IStripeCustomerService
{
    Task<string> UpsertCustomerAsync(Guid userId, string email, string name, CancellationToken cancellationToken = default);

    Task UpdateCustomerAsync(string stripeCustomerId, string email, string name, CancellationToken cancellationToken = default);

    Task DeactivateCustomerAsync(string stripeCustomerId, CancellationToken cancellationToken = default);
}
