namespace Bookify.Infrastructure.Security;

public sealed class TurnstileOptions
{
    public Uri BaseUrl { get; set; } = null!;
    public string SiteKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
}
