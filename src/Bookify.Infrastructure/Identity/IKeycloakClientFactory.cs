using NETCore.Keycloak.Client.HttpClients.Abstraction;

namespace Bookify.Infrastructure.Identity;

internal interface IKeycloakClientFactory
{
    Task<IKeycloakClient> GetClientAsync();
    Task<string?> GetAccessToken();
    string GetRealm();
}
