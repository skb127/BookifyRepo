using Bookify.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id);

        builder.HasData(Permission.UsersRead);
        builder.HasData(Permission.UsersAdminRead);
        builder.HasData(Permission.ApartmentsWrite);
        builder.HasData(Permission.BookingsWrite);
        builder.HasData(Permission.BookingsRead);
        builder.HasData(Permission.ReviewsRead);
    }
}
