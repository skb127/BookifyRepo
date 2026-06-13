using Bookify.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber)
            .HasConversion(n => n.Value, v => InvoiceNumber.FromValue(v))
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.InvoiceType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.IssueDate)
            .IsRequired();

        builder.Property(i => i.TotalAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(i => i.TaxAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(i => i.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(i => i.PdfUrl)
            .HasMaxLength(2048)
            .IsRequired(false);

        builder.Property(i => i.CreatedOnUtc)
            .IsRequired();

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(i => i.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(i => i.OriginalInvoiceId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(i => i.BookingId);
    }
}
