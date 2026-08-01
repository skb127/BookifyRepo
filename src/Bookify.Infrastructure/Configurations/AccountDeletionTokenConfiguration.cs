using Bookify.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

internal sealed class AccountDeletionTokenConfiguration : IEntityTypeConfiguration<AccountDeletionToken>
{
    public void Configure(EntityTypeBuilder<AccountDeletionToken> builder)
    {
        builder.ToTable("account_deletion_tokens");

        builder.HasKey(adt => adt.Id);

        builder.Property(adt => adt.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(adt => adt.ExpirationUtc)
            .IsRequired();

        builder.HasIndex(adt => adt.TokenHash)
            .IsUnique();
    }
}
