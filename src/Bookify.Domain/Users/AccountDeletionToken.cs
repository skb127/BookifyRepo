using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users;

public sealed class AccountDeletionToken : Entity
{
    // Empty constructor for EF Core
    private AccountDeletionToken()
    {

    }

    private AccountDeletionToken(Guid userId, string tokenHash, DateTimeOffset expirationUtc)
        : base(Guid.CreateVersion7())
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpirationUtc = expirationUtc;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpirationUtc { get; private set; }

    public bool IsExpired => DateTimeOffset.UtcNow > ExpirationUtc;

    public static AccountDeletionToken Create(Guid userId, string tokenHash, DateTimeOffset expirationUtc) =>
        new(userId, tokenHash, expirationUtc);
}
