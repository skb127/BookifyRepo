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

        builder.OwnsOne(booking => booking.PriceForPeriod, priceBuilder => priceBuilder.Property(money => money.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code)));

        builder.OwnsOne(booking => booking.CleaningFee, feeBuilder => feeBuilder.Property(money => money.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code)));

        builder.OwnsOne(booking => booking.AmenitiesUpCharge, upChargeBuilder => upChargeBuilder.Property(money => money.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code)));

        builder.OwnsOne(booking => booking.TotalPrice, totalPriceBuilder => totalPriceBuilder.Property(money => money.Currency)
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

    }
}
