using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users;

public sealed class PasswordResetToken : Entity
{
    // Empty constructor for the EF
    private PasswordResetToken()
    {

    }

    private PasswordResetToken(Guid userId, string tokenHash, DateTimeOffset expirationUtc)
        : base(Guid.CreateVersion7()) // Generate the ID for the entity itself
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpirationUtc = expirationUtc;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpirationUtc { get; private set; }

    public bool IsExpired => DateTimeOffset.UtcNow > ExpirationUtc;
    
    public static PasswordResetToken Create(Guid userId, string tokenHash, DateTimeOffset expirationUtc) =>
        new(userId, tokenHash, expirationUtc);

    internal void Update(string tokenHash, DateTimeOffset expirationUtc)
    {
        TokenHash = tokenHash;
        ExpirationUtc = expirationUtc;
    }
}
