using System.Net.Http.Json;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Users;
using Bookify.Domain.Abstractions;
using Bookify.Infrastructure.Authentication.Models;
using Microsoft.Extensions.Options;

namespace Bookify.Infrastructure.Authentication;

internal sealed class JwtService : IJwtService
{
    private const string PasswordGrantType = "password";
    private const string RefreshGrantType = "refresh_token";

    private const string TokenEndpoint = "token";
    private const string RevokeEndpoint = "revoke";
    
    private static readonly Error AuthenticationFailed = new(
        "Keycloak.AuthenticationFailed",
        "The access token could not be obtained due to authentication");
    private static readonly Error TokenRevoked = new(
        "Keycloak.TokenRevoked",
        "The token could not be revoked");

    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _keycloakOptions;

    public JwtService(HttpClient httpClient, IOptions<KeycloakOptions> keycloakOptions)
    {
        _httpClient = httpClient;
        _keycloakOptions = keycloakOptions.Value;
    }

    public async Task<Result<AccessTokenResponse>> GetAccessTokenAsync(string email, 
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var authRequestParameters = new KeyValuePair<string, string>[]
            {
                new("client_id", _keycloakOptions.AuthClientId),
                new("client_secret", _keycloakOptions.AuthClientSecret),
                new("scope", "openid email"),
                new("grant_type", PasswordGrantType),
                new("username", email),
                new("password", password)
            };

            using var authorizationRequestContent = new FormUrlEncodedContent(authRequestParameters);

            HttpResponseMessage response = await _httpClient.PostAsync(new Uri(TokenEndpoint, UriKind.Relative), authorizationRequestContent, cancellationToken);

            response.EnsureSuccessStatusCode();

            AuthorizationToken? authorizationToken = await response.Content.ReadFromJsonAsync<AuthorizationToken>(cancellationToken);

            if (authorizationToken is null)
            {
                return Result.Failure<AccessTokenResponse>(AuthenticationFailed);
            }

            var result = new AccessTokenResponse(
                authorizationToken.AccessToken,
                authorizationToken.RefreshToken,
                authorizationToken.ExpiresIn,
                authorizationToken.RefreshExpiresIn);

            return Result.Success(result);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<AccessTokenResponse>(AuthenticationFailed);
        } 
    }

    public async Task<Result<AccessTokenResponse>> GetRefreshTokenAsync(string refreshToken, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var authRequestParameters = new KeyValuePair<string, string>[]
            {
                new("client_id", _keycloakOptions.AuthClientId),
                new("client_secret", _keycloakOptions.AuthClientSecret),
                new("grant_type", RefreshGrantType),
                new("refresh_token", refreshToken)
            };

            using var authorizationRequestContent = new FormUrlEncodedContent(authRequestParameters);

            HttpResponseMessage response = await _httpClient.PostAsync(new Uri(TokenEndpoint, UriKind.Relative), authorizationRequestContent, cancellationToken);

            response.EnsureSuccessStatusCode();

            AuthorizationToken? authorizationToken = await response.Content.ReadFromJsonAsync<AuthorizationToken>(cancellationToken);

            if (authorizationToken is null)
            {
                return Result.Failure<AccessTokenResponse>(AuthenticationFailed);
            }

            var result = new AccessTokenResponse(
                authorizationToken.AccessToken,
                authorizationToken.RefreshToken,
                authorizationToken.ExpiresIn,
                authorizationToken.RefreshExpiresIn);

            return Result.Success(result);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<AccessTokenResponse>(AuthenticationFailed);
        }
    }

    public async Task<Result> RevokeUserAsync(string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var revokeRequestParameters = new KeyValuePair<string, string>[]
            {
                new("client_id", _keycloakOptions.AuthClientId),
                new("client_secret", _keycloakOptions.AuthClientSecret),
                new("token", refreshToken),
                new("token_type_hint", "refresh_token")
            };

            using var revokeRequestContent = new FormUrlEncodedContent(revokeRequestParameters);

            HttpResponseMessage response = await _httpClient.PostAsync(new Uri(RevokeEndpoint, UriKind.Relative), revokeRequestContent, cancellationToken);

            response.EnsureSuccessStatusCode();

            return Result.Success();
        }
        catch (HttpRequestException)
        {
            return Result.Failure(TokenRevoked);
        }
    }
}
