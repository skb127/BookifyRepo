using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(booking => booking.Id);

        builder.OwnsOne(booking => booking.PriceForPeriod, priceBuilder => priceBuilder
            .Property(money => money.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code)));

        builder.OwnsOne(booking => booking.CleaningFee, feeBuilder => feeBuilder.Property(money => money.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code)));

        builder.OwnsOne(booking => booking.AmenitiesUpCharge, upChargeBuilder => upChargeBuilder
            .Property(money => money.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code)));

        builder.OwnsOne(booking => booking.TotalPrice, totalPriceBuilder => totalPriceBuilder
            .Property(money => money.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code)));


        builder.OwnsOne(booking => booking.ExtraGuestCharge, extraGuestChargeBuilder =>
        {
            extraGuestChargeBuilder.Property(money => money.Amount)
                .HasDefaultValue(0);

            extraGuestChargeBuilder.Property(money => money.Currency)
                .HasConversion(currency => currency.Code, code => Currency.FromCode(code));
        });

        builder.Property(booking => booking.GuestCount)
            .HasDefaultValue(1)
            .IsRequired();

        builder.OwnsOne(booking => booking.Duration);

        // A booking is associated with one apartment, and an apartment can have many bookings
        builder.HasOne<Apartment>()
            .WithMany()
            .HasForeignKey(booking => booking.ApartmentId);

        // A booking is associated with one user, and a user can have many bookings
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(booking => booking.UserId);

        builder.Property(booking => booking.CompletedNotificationSentAt)
            .IsRequired(false);

        builder.Property(booking => booking.PaymentStatus)
            .HasConversion<int>();

        builder.Property(booking => booking.ExpiresAt)
            .IsRequired(false);

        builder.Property(booking => booking.CheckedInOnUtc)
            .IsRequired(false);

        builder.Property(booking => booking.NoShowAt)
            .IsRequired(false);

        builder.Property(booking => booking.ExpiredOnUtc)
            .IsRequired(false);

        // Reasons for cancelling or no-showing a booking
        builder.OwnsMany(booking => booking.Reasons, reasonBuilder =>
        {
            reasonBuilder.ToTable("booking_reasons");
            reasonBuilder.WithOwner().HasForeignKey("booking_id");
            reasonBuilder.Property<Guid>("Id").ValueGeneratedOnAdd();
            reasonBuilder.HasKey("Id");

            reasonBuilder.Property(r => r.Type)
                .HasColumnName("reason_type")
                .HasConversion<int>()
                .IsRequired();

            reasonBuilder.Property(r => r.Description)
                .HasColumnName("description")
                .HasMaxLength(500)
                .IsRequired(false);

            reasonBuilder.Property(r => r.CreatedOnUtc)
                .HasColumnName("created_on_utc")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        builder.Property<string>("status_literal")
            .HasMaxLength(50)
            .HasDefaultValue("Reserved")
            .IsRequired();

        builder.Property<string>("payment_status_literal")
            .HasMaxLength(50)
            .HasDefaultValue("Unpaid")
            .IsRequired();

        // Taxes applied to the booking (calculated when applied)
        builder.OwnsMany(booking => booking.Taxes, taxBuilder =>
        {
            taxBuilder.ToTable("booking_taxes");
            taxBuilder.WithOwner().HasForeignKey("booking_id");
            taxBuilder.Property(bt => bt.Id).ValueGeneratedNever();
            taxBuilder.HasKey(bt => bt.Id);

            taxBuilder.Property(bt => bt.TaxRuleName)
                .HasColumnName("tax_rule_name")
                .HasMaxLength(200)
                .IsRequired();

            taxBuilder.Property(bt => bt.TaxType)
                .HasColumnName("tax_type")
                .HasConversion<int>()
                .IsRequired();

            taxBuilder.Property(bt => bt.Rate)
                .HasColumnName("rate")
                .HasPrecision(18, 4)
                .IsRequired();

            taxBuilder.OwnsOne(bt => bt.CalculatedAmount, amountBuilder =>
            {
                amountBuilder.Property(money => money.Amount)
                    .HasColumnName("calculated_amount_amount")
                    .HasPrecision(18, 4)
                    .IsRequired();

                amountBuilder.Property(money => money.Currency)
                    .HasColumnName("calculated_amount_currency")
                    .HasConversion(currency => currency.Code, code => Currency.FromCode(code))
                    .HasMaxLength(3)
                    .IsRequired();
            });

            taxBuilder.Property(bt => bt.CreatedOnUtc)
                .HasColumnName("created_on_utc")
                .IsRequired();
        });

        // Refund for cancelled bookings (not all cancellations will have a refund)
        builder.OwnsOne(booking => booking.Refund, refundBuilder =>
        {
            refundBuilder.ToTable("booking_refunds");
            refundBuilder.WithOwner().HasForeignKey("booking_id");
            refundBuilder.Property<Guid>("Id").ValueGeneratedNever();
            refundBuilder.HasKey("Id");

            refundBuilder.Property(r => r.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 4)
                .IsRequired();

            refundBuilder.Property(r => r.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();

            refundBuilder.Property(r => r.Reason)
                .HasColumnName("reason")
                .HasMaxLength(200)
                .IsRequired();

            refundBuilder.Property(r => r.InitiatedOnUtc)
                .HasColumnName("initiated_on_utc")
                .IsRequired();
        });
    }
}
