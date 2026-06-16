using Bookify.Application.Abstractions.Payments;
using Stripe;

namespace Bookify.Infrastructure.Payments;

internal sealed class StripeCustomerService : IStripeCustomerService
{
    private readonly IStripeClient _stripeClient;

    public StripeCustomerService(IStripeClient stripeClient) => _stripeClient = stripeClient;

    public async Task<string> UpsertCustomerAsync(
        Guid userId,
        string email,
        string name,
        CancellationToken cancellationToken = default)
    {
        var customerService = new CustomerService(_stripeClient);

        var searchOptions = new CustomerSearchOptions
        {
            Query = $"metadata['bookify_user_id']:'{userId}'",
            Limit = 1
        };

        StripeSearchResult<Customer> searchResult = await customerService.SearchAsync(searchOptions, cancellationToken: cancellationToken);
        Customer? customer = searchResult.Data.FirstOrDefault();

        if (customer is not null)
        {
            if (customer.Metadata.TryGetValue("status", out string? status) && status == "deleted")
            {
                var updateOptions = new CustomerUpdateOptions
                {
                    Email = email,
                    Name = name,
                    Metadata = new Dictionary<string, string>
                    {
                        { "status", "active" }
                    }
                };

                customer = await customerService.UpdateAsync(customer.Id, updateOptions, cancellationToken: cancellationToken);
            }
            else
            {
                var updateOptions = new CustomerUpdateOptions
                {
                    Email = email,
                    Name = name
                };

                customer = await customerService.UpdateAsync(customer.Id, updateOptions, cancellationToken: cancellationToken);
            }

            return customer.Id;
        }

        var createOptions = new CustomerCreateOptions
        {
            Email = email,
            Name = name,
            Metadata = new Dictionary<string, string>
            {
                { "bookify_user_id", userId.ToString() },
                { "status", "active" }
            }
        };

        Customer newCustomer = await customerService.CreateAsync(createOptions, cancellationToken: cancellationToken);

        return newCustomer.Id;
    }

    public async Task UpdateCustomerAsync(
        string stripeCustomerId,
        string email,
        string name,
        CancellationToken cancellationToken = default)
    {
        var customerService = new CustomerService(_stripeClient);

        var updateOptions = new CustomerUpdateOptions
        {
            Email = email,
            Name = name
        };

        await customerService.UpdateAsync(stripeCustomerId, updateOptions, cancellationToken: cancellationToken);
    }

    public async Task DeactivateCustomerAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken = default)
    {
        var customerService = new CustomerService(_stripeClient);

        var updateOptions = new CustomerUpdateOptions
        {
            Metadata = new Dictionary<string, string>
            {
                { "status", "deleted" }
            }
        };

        await customerService.UpdateAsync(stripeCustomerId, updateOptions, cancellationToken: cancellationToken);
    }
}
