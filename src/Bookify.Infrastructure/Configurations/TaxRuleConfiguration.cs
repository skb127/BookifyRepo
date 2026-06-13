using Bookify.Domain.TaxRules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

internal sealed class TaxRuleConfiguration : IEntityTypeConfiguration<TaxRule>
{
    public void Configure(EntityTypeBuilder<TaxRule> builder)
    {
        builder.ToTable("tax_rules");

        builder.HasKey(tr => tr.Id);

        builder.Property(tr => tr.CountryCode)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(tr => tr.Region)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(tr => tr.City)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(tr => tr.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(tr => tr.EffectiveFrom)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(tr => tr.EffectiveTo)
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(tr => tr.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(tr => tr.CreatedOnUtc)
            .IsRequired();

        builder.Property(tr => tr.UpdatedOnUtc)
            .IsRequired(false);

        builder.OwnsOne(tr => tr.Rate, rateBuilder =>
        {
            rateBuilder.Property(r => r.Value)
                .HasColumnName("rate_value")
                .HasPrecision(18, 4)
                .IsRequired();

            rateBuilder.Property(r => r.Type)
                .HasColumnName("rate_type")
                .HasConversion<int>()
                .IsRequired();
        });

        builder.HasIndex(tr => new { tr.CountryCode, tr.IsActive });
    }
}
