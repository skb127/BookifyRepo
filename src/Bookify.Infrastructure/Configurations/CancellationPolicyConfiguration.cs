using Bookify.Domain.CancellationPolicies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

internal sealed class CancellationPolicyConfiguration : IEntityTypeConfiguration<CancellationPolicy>
{
    public void Configure(EntityTypeBuilder<CancellationPolicy> builder)
    {
        builder.ToTable("cancellation_policies");

        builder.HasKey(cp => cp.Id);

        builder.Property(cp => cp.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(cp => cp.EarlyGuestPenaltyRate)
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(cp => cp.LateGuestPenaltyRate)
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(cp => cp.EarlyHostPenaltyRate)
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(cp => cp.LateHostPenaltyRate)
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(cp => cp.ThresholdHours)
            .IsRequired();

        builder.Property(cp => cp.IsDefault)
            .IsRequired();

        builder.Property(cp => cp.CreatedOnUtc)
            .IsRequired();

        builder.HasIndex(cp => cp.IsDefault)
            .IsUnique()
            .HasFilter("is_default = true");
    }
}
