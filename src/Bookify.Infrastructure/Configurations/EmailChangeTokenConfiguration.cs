using Bookify.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

public class EmailChangeTokenConfiguration : IEntityTypeConfiguration<EmailChangeToken>
{
    public void Configure(EntityTypeBuilder<EmailChangeToken> builder)
    {
        builder.ToTable("email_change_tokens");

        builder.HasKey(ect => ect.Id);

        builder.Property(ect => ect.TokenHash)
            .HasMaxLength(64) // SHA-256 Hex string length
            .IsRequired();

        builder.Property(ect => ect.PendingEmail)
            .HasMaxLength(400)
            .HasConversion(email => email.Value, value => new Domain.Users.Email(value))
            .IsRequired();

        builder.Property(ect => ect.ExpirationUtc)
            .IsRequired();

        builder.HasIndex(ect => ect.TokenHash)
            .IsUnique();
    }
}
