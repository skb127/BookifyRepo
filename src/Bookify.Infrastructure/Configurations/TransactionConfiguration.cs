using Bookify.Domain.Bookings;
using Bookify.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.StripeSessionId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.StripePaymentIntentId)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(t => t.CheckoutSessionUrl)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(t => t.ProviderStatus)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.CreatedOnUtc)
            .IsRequired();

        builder.Property(t => t.UpdatedOnUtc)
            .IsRequired(false);

        builder.OwnsOne(t => t.Amount, amountBuilder =>
        {
            amountBuilder.Property(money => money.Amount)
                .HasColumnName("amount_amount")
                .HasPrecision(18, 4)
                .IsRequired();

            amountBuilder.Property(money => money.Currency)
                .HasColumnName("amount_currency")
                .HasConversion(currency => currency.Code, code => Currency.FromCode(code))
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(t => t.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.StripeSessionId)
            .IsUnique();
    }
}
