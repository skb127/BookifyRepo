using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.Users;

namespace Bookify.Application.IntegrationTests.Infrastructure;

[Collection("RateLimitTestCollection")]
public abstract class RateLimitIntegrationTest
{
    protected HttpClient HttpClient { get; }
    protected RateLimitTestWebAppFactory Factory { get; }

    protected RateLimitIntegrationTest(RateLimitTestWebAppFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Factory = factory;
        HttpClient = factory.CreateClient();
        
        // Assign a unique simulated IP to each test to avoid collisions in the GlobalLimiter
        HttpClient.DefaultRequestHeaders.Add("X-Test-IP", Guid.NewGuid().ToString());
        HttpClient.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");
    }

    protected async Task<string> GetAccessTokenAsync(string email, string password)
    {
        // Use a client with bypass header so login requests don't consume write-operations permits
        using var bypassClient = Factory.CreateClient();
        bypassClient.DefaultRequestHeaders.Add("X-Test-Bypass-RateLimit", "true");
        bypassClient.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");
        
        var response = await bypassClient.PostAsJsonAsync("api/v1/users/login", new LoginUserRequest(email, password))
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<AccessTokenOnlyResponse>()
            .ConfigureAwait(false);
        return tokenResponse!.AccessToken;
    }
}