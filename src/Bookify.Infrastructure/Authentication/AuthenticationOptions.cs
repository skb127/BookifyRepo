namespace Bookify.Infrastructure.Authentication;

public sealed class AuthenticationOptions
{
    public string Audience { get; init; } = "";

    public Uri MetadataUrl { get; set; } = null!;

    public bool RequiredHttpsMetadata { get; init; } 

    public string Issuer { get; set; } = "";
}
