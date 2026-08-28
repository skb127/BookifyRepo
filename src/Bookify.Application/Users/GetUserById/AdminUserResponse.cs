namespace Bookify.Application.Users.GetUserById;

public sealed record AdminUserResponse
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateOnly DateOfBirth { get; init; }
    public string? PhoneNumber { get; init; }
    public string IdentityId { get; init; } = string.Empty;
    public string? StripeCustomerId { get; init; }
    public string StatusCode { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public DateTime? DeletedAt { get; init; }
    public DateTime? DeletionScheduledAt { get; init; }
    public DateTime? LastModifiedOn { get; init; }
    public int BanCount { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}
