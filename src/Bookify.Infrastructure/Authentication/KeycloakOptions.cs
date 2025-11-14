namespace Bookify.Infrastructure.Authentication;

public sealed class KeycloakOptions
{
    public string AdminUrl { get; init; } = "";
    public string TokenUrl { get; init; } = "";
    public string AdminClientId { get; init; } = "";
    public string AdminClientSecret { get; init; } = "";
    public string AuthClientId { get; init; } = "";
    public string AuthClientSecret { get; init; } = "";
}
