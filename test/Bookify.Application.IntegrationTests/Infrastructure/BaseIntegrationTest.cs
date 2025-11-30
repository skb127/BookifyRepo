using Bookify.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Application.IntegrationTests.Infrastructure;

public abstract class BaseIntegrationTest : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IServiceScope _scope; // To allow resolving scoped services
    protected readonly ISender Sender; // To send commands/queries via MediatR
    protected readonly ApplicationDbContext DbContext; // To interact with the database

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        _scope = factory.Services.CreateScope();

        Sender = _scope.ServiceProvider.GetRequiredService<ISender>();
        DbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}
