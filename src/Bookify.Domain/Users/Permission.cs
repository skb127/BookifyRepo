namespace Bookify.Domain.Users;

public sealed class Permission
{
    // An alternative would be using enums to define the roles and permissions, but I prefer to manage
    // them from the database for more flexibility. Updating permissions would not require code changes
    public static readonly Permission UsersRead = new(1, "users:read");

    public Permission(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; init; }

    public string Name { get; init; }
}
