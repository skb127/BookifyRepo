using Bookify.Application.Abstractions.Caching;
using Bookify.Infrastructure.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NETCore.Keycloak.Client.HttpClients.Abstraction;
using NETCore.Keycloak.Client.HttpClients.Implementation;
using NETCore.Keycloak.Client.Models;
using NETCore.Keycloak.Client.Models.Auth;
using NETCore.Keycloak.Client.Models.Tokens;

namespace Bookify.Infrastructure.Identity;

internal sealed class KeycloakClientFactory : IKeycloakClientFactory, IDisposable
{
    private readonly KeycloakOptions _options;
    private KeycloakClient? _client;
    private readonly ICacheService _cacheService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<KeycloakClientFactory> _logger;

    public KeycloakClientFactory(
        IOptions<KeycloakOptions> options,
        ICacheService cacheService,
        ILogger<KeycloakClientFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<IKeycloakClient> GetClientAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _client ??= new KeycloakClient(_options.BaseUrl.AbsoluteUri);

            if (await _cacheService.GetAsync<string>("admin:accessToken") is null)
            {
                await RefreshTokenAsync();
            }

            return _client;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task RefreshTokenAsync()
    {
        try
        {
            string realm = _options.Realm;

            _logger.LogDebug(
                "Obtaining Keycloak admin access token. Realm: {Realm}, ClientId: {ClientId}",
                realm,
                _options.AdminClientId);

            ArgumentNullException.ThrowIfNull(_client);

            KcResponse<KcIdentityProviderToken>? tokenResponse = await _client.Auth.GetClientCredentialsTokenAsync(
                realm,
                new KcClientCredentials
                {
                    ClientId = _options.AdminClientId,
                    Secret = _options.AdminClientSecret
                });

            if (tokenResponse is { IsError: true })
            {
                throw new InvalidOperationException(tokenResponse.ErrorMessage);
            }

            await _cacheService.SetAsync("admin:accessToken", tokenResponse.Response.AccessToken,
                TimeSpan.FromSeconds(tokenResponse.Response.ExpiresIn));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to obtain Keycloak admin access token: {ErrorMessage}",
                ex.Message);
            throw new InvalidOperationException(
                "Unable to initialize Keycloak admin client. Check the logs for more details.", ex);
        }
    }

    public async Task<string?> GetAccessToken() =>
        await _cacheService.GetAsync<string>("admin:accessToken") ??
        throw new InvalidOperationException("Unable to obtain Keycloak admin access token");

    public string GetRealm() =>
        _options.Realm;

    public void Dispose() => _semaphore.Dispose();
}
