namespace Bookify.Domain.Users;

// I'm doing this because I can see these values from the EF core migration.
// But also I could manage this directly from the DB 
public class RolePermission
{
    public int RoleId { get; set; }

    public int PermissionId { get; set; }
}
