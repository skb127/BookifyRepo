namespace Bookify.Domain.Users;

public record UserStatus
{
    internal static readonly UserStatus None = new('U', "Unknown");
    public static readonly UserStatus Active = new('A', "Active");
    public static readonly UserStatus Inactive = new('I', "Inactive");
    public static readonly UserStatus Suspended = new('S', "Suspended");
    public static readonly UserStatus Deleted = new('D', "Deleted");
    public static readonly UserStatus PendingDeletion = new('P', "PendingDeletion");

    private UserStatus(char code, string name)
    {
        Code = code;
        Name = name;
    }

    public char Code { get; init; }
    public string Name { get; init; }

    public static UserStatus FromCode(char code) =>
        All.FirstOrDefault(s => s.Code == code) ??
        throw new InvalidOperationException("The user status code is invalid");

    public static readonly IReadOnlyCollection<UserStatus> All =
    [
        Active,
        Inactive,
        Suspended,
        Deleted,
        PendingDeletion
    ];
}
