using Bookify.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(prt => prt.Id);

        builder.Property(prt => prt.TokenHash)
            .HasMaxLength(64) // SHA-256 Hex string length
            .IsRequired();

        builder.Property(prt => prt.ExpirationUtc)
            .IsRequired();

        builder.HasIndex(prt => prt.TokenHash)
            .IsUnique();
    }
}
