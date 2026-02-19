using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users;

public sealed class EmailChangeToken : Entity
{
    // Empty constructor for EF
    private EmailChangeToken()
    {
    }

    private EmailChangeToken(Guid userId, Email pendingEmail, string tokenHash, DateTimeOffset expirationUtc)
        : base(Guid.CreateVersion7())
    {
        UserId = userId;
        PendingEmail = pendingEmail;
        TokenHash = tokenHash;
        ExpirationUtc = expirationUtc;
    }

    public Guid UserId { get; private set; }
    public Email PendingEmail { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpirationUtc { get; private set; }

    public bool IsExpired => DateTimeOffset.UtcNow > ExpirationUtc;

    public static EmailChangeToken Create(Guid userId, Email pendingEmail, string tokenHash, DateTimeOffset expirationUtc) =>
        new(userId, pendingEmail, tokenHash, expirationUtc);
    
    internal void Update(Email pendingEmail, string tokenHash, DateTimeOffset expirationUtc)
    {
        PendingEmail = pendingEmail;
        TokenHash = tokenHash;
        ExpirationUtc = expirationUtc;
    }
}
