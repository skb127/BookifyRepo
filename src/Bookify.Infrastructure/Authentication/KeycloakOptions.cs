namespace Bookify.Infrastructure.Authentication;

public sealed class KeycloakOptions
{
    public Uri BaseUrl { get; set; } = null!;
    public string Realm { get; init; } = "";
    public Uri AdminUrl { get; set; } = null!;
    public Uri TokenUrl { get; set; } = null!;
    public Uri OidcBaseUrl { get; set; } = null!;
    public string AdminClientId { get; init; } = "";
    public string AdminClientSecret { get; init; } = "";
    public string AuthClientId { get; init; } = "";
    public string AuthClientSecret { get; init; } = "";
}
