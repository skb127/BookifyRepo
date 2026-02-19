namespace Bookify.Application.Options;

public sealed class ExpirationOptions
{
    public int EmailChangeExpirationSeconds { get; set; } = 1800; // 30 minutes
    public int PasswordRecoveryExpirationSeconds { get; set; } = 600; // 10 minutes
}
