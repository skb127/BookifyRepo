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
    }
}
