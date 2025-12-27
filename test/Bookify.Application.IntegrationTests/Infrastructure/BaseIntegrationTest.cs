using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.Users;
using Bookify.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Application.IntegrationTests.Infrastructure;

public abstract class BaseIntegrationTest : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IServiceScope _scope; // To allow resolving scoped services
    protected ISender Sender { get; } // To send commands/queries via MediatR
    protected ApplicationDbContext DbContext { get; }  // To interact with the database
    protected HttpClient HttpClient { get; } // To make HTTP requests to the test server

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        
        _scope = factory.Services.CreateScope();

        Sender = _scope.ServiceProvider.GetRequiredService<ISender>();
        DbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        HttpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }
    
    protected async Task<string> GetAccessToken(string userEmail, string userPassword)
    {
        HttpResponseMessage loginResponse = await HttpClient.PostAsJsonAsync(
            "api/v1/users/login",
            new LoginUserRequest(
                userEmail,
                userPassword)).ConfigureAwait(false);

        AccessTokenOnlyResponse? accessTokenResponse = await loginResponse.Content.ReadFromJsonAsync<AccessTokenOnlyResponse>().ConfigureAwait(false);

        return accessTokenResponse?.AccessToken ?? throw new InvalidOperationException("Unable to get access token");
    }
}
