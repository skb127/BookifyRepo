using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Api.FunctionalTests.Users;
using Bookify.Application.Users;

namespace Bookify.Api.FunctionalTests.Infrastructure;

public abstract class BaseFunctionalTest : IClassFixture<FunctionalTestWebAppFactory>
{
    protected HttpClient HttpClient { get; }

    protected BaseFunctionalTest(FunctionalTestWebAppFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        
        HttpClient = factory.CreateClient();
    }

    protected async Task<string> GetAccessToken(string userEmail, string userPassword)
    {
        HttpResponseMessage loginResponse = await HttpClient.PostAsJsonAsync(
            "api/v1/users/login",
            new LoginUserRequest(
                userEmail,
                userPassword)).ConfigureAwait(false);

        AccessTokenResponse? accessTokenResponse = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>().ConfigureAwait(false);

        return accessTokenResponse?.AccessToken ?? throw new InvalidOperationException("Unable to get access token");
    }
}
