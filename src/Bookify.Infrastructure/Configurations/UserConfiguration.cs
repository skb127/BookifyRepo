using Bookify.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.FirstName)
            .HasMaxLength(200)
            .HasConversion(firstName => firstName.Value, value => new FirstName(value));

        builder.Property(user => user.LastName)
            .HasMaxLength(200)
            .HasConversion(lastName => lastName.Value, value => new LastName(value));

        builder.Property(user => user.Email)
            .HasMaxLength(400)
            .HasConversion(email => email.Value, value => new Domain.Users.Email(value));

        builder.Property(user => user.Status)
            .HasMaxLength(1)
            .HasConversion(status => status.Code, code => UserStatus.FromCode(code))
            .HasDefaultValueSql($"'{UserStatus.Active.Code}'");
        
        builder.HasIndex(user => user.Email)
            .IsUnique(); // We are defining an index on the email property, this is a unique index, this is going to give us a database guaranteed constraint.

        builder.HasIndex(user => user.IdentityId)
            .IsUnique();

        builder.HasQueryFilter(user => user.Status != UserStatus.Deleted);
    }
}
