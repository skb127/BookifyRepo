using Bookify.Domain.Bookings;
using Bookify.Domain.Users;
using Bookify.Infrastructure.HostBalances;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

internal sealed class HostBalanceConfiguration : IEntityTypeConfiguration<HostBalance>
{
    public void Configure(EntityTypeBuilder<HostBalance> builder)
    {
        builder.ToTable("host_balances");

        builder.HasKey(hb => hb.Id);

        builder.Property(hb => hb.Amount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(hb => hb.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(hb => hb.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(hb => hb.CreatedOnUtc)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(hb => hb.HostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(hb => hb.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
