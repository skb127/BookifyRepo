using System.Net.Http.Json;
using Bookify.Api.Controllers.Users.Requests;
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

        // Warm up the test server pipeline, JIT compilation, and Keycloak OIDC metadata lookup
        // by making a bypassed request before starting the actual time-sensitive tests.
        try
        {
            using var warmupClient = Factory.CreateClient();
            warmupClient.DefaultRequestHeaders.Add("X-Test-Bypass-RateLimit", "true");
            warmupClient.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");
            warmupClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "warmup-dummy-token");
            _ = warmupClient.GetAsync(new Uri("api/v1/users/me", UriKind.Relative)).GetAwaiter().GetResult();
        }
        catch
        {
            // Suppress errors to avoid failing the test class instantiation if Keycloak is temporarily busy
        }
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