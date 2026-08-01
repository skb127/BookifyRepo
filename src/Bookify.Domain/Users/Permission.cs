namespace Bookify.Domain.Users;

public sealed class Permission
{
    // An alternative would be using enums to define the roles and permissions, but I prefer to manage
    // them from the database for more flexibility. Updating permissions would not require code changes
    public static readonly Permission UsersRead = new(1, "users:read");
    public static readonly Permission UsersAdminRead = new(2, "users:admin-read");
    public static readonly Permission ApartmentsWrite = new(3, "apartments:write");
    public static readonly Permission BookingsWrite = new(4, "bookings:write");
    public static readonly Permission BookingsRead = new(5, "bookings:read");
    public static readonly Permission ReviewsRead = new(6, "reviews:read");
    public static readonly Permission UsersAdminWrite = new(7, "users:admin-write");
    public static readonly Permission UsersBan = new(8, "users:ban");

    public Permission(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; init; }

    public string Name { get; init; }
}
