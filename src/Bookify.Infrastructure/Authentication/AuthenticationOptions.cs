namespace Bookify.Infrastructure.Authentication;

public sealed class AuthenticationOptions
{
    public string Audience { get; init; } = "";

    public string MetadataUrl { get; init; } = "";

    public bool RequiredHttpsMetadata { get; init; } 

    public string Issuer { get; set; } = "";
}
