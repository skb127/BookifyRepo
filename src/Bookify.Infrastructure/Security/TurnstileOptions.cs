namespace Bookify.Infrastructure.Security;

public sealed class TurnstileOptions
{
    public Uri BaseUrl { get; init; } = null!;
    public string SiteKey { get; init; } = "";
    public string SecretKey { get; init; } = "";
}
