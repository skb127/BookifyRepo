using Bookify.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permission");

        builder.HasKey(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId });

        builder.HasData(
            new RolePermission
            {
                RoleId = Role.Registered.Id,
                PermissionId = Permission.UsersRead.Id
            },
            new RolePermission
            {
                RoleId = Role.Admin.Id,
                PermissionId = Permission.UsersRead.Id
            },
            new RolePermission
            {
                RoleId = Role.Admin.Id,
                PermissionId = Permission.UsersAdminRead.Id
            },
            new RolePermission
            {
                RoleId = Role.Admin.Id,
                PermissionId = Permission.ApartmentsWrite.Id
            },
            new RolePermission
            {
                RoleId = Role.Admin.Id,
                PermissionId = Permission.BookingsWrite.Id
            },
            new RolePermission
            {
                RoleId = Role.Admin.Id,
                PermissionId = Permission.BookingsRead.Id
            },
            new RolePermission
            {
                RoleId = Role.Admin.Id,
                PermissionId = Permission.ReviewsRead.Id
            });
    }
}
