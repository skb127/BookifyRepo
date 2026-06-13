using Bookify.Domain.Shared;
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

        builder.Property(user => user.PhoneNumber)
            .HasMaxLength(20)
            .HasConversion(phoneNumber => phoneNumber != null ? phoneNumber.Value : null, value => PhoneNumber.Create(value!));

        builder.Property(user => user.DateOfBirth)
            .HasConversion(dateOfBirth => dateOfBirth.Value, value => DateOfBirth.Create(value))
            .HasDefaultValue(DateOfBirth.Create(new DateOnly(1900, 1, 1)));

        builder.Property(user => user.LastModifiedOn);

        builder.HasIndex(user => user.Email)
            .IsUnique(); // We are defining an index on the email property, this is a unique index, this is going to give us a database guaranteed constraint.

        builder.HasIndex(user => user.IdentityId)
            .IsUnique();

        builder.HasOne(user => user.PasswordResetToken)
            .WithOne()
            .HasForeignKey<PasswordResetToken>(prt => prt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(user => user.EmailChangeToken)
            .WithOne()
            .HasForeignKey<EmailChangeToken>(ect => ect.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(user => user.StripeCustomerId)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.HasQueryFilter(user => user.Status != UserStatus.Deleted);
    }
}
