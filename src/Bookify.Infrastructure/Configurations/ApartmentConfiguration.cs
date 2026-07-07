using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;
using Bookify.Domain.CancellationPolicies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations;

internal sealed class ApartmentConfiguration : IEntityTypeConfiguration<Apartment>
{
    public void Configure(EntityTypeBuilder<Apartment> builder)
    {
        builder.ToTable("apartments");

        builder.HasKey(apartment => apartment.Id);

        builder.Property(apartment => apartment.OwnerId)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(apartment => apartment.OwnerId)
            .OnDelete(DeleteBehavior.Restrict); // Prevent deleting a User if they still own Apartments

        builder.HasOne<CancellationPolicy>()
            .WithMany()
            .HasForeignKey(apartment => apartment.CancellationPolicyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(apartment =>
            apartment.Address); // The value object is going to be mapped into a set of columns in the same table as the owning entity, in this case the Address columns are going to be in the apartments table

        builder.Property(apartment => apartment.Name)
            .HasMaxLength(200)
            .HasConversion(name => name.Value,
                value => new Name(
                    value)); // Converting the Name value object to its underlying string value for storage in the database and vice versa

        builder.Property(apartment => apartment.Description)
            .HasMaxLength(2000)
            .HasConversion(description => description.Value, value => new Description(value));

        builder.OwnsOne(apartment => apartment.Price, priceBuilder => priceBuilder.Property(money => money.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code)));

        builder.OwnsOne(apartment => apartment.CleaningFee, priceBuilder => priceBuilder
            .Property(money => money.Currency)
            .HasConversion(currency => currency.Code, code => Currency.FromCode(code)));

        builder.Property(apartment => apartment.Amenities)
            .HasColumnType("integer[]")
            .HasColumnName("amenities");

        builder.Property(apartment => apartment.DeletedAt);

        builder.Property(apartment => apartment.InstantBooking)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(apartment => apartment.MinimumNights)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(apartment => apartment.CheckInCutOffHours)
            .HasDefaultValue(3)
            .IsRequired();

        builder.Property<uint>("Version").IsRowVersion(); // Shadow property for optimistic concurrency control

        builder.HasQueryFilter(apartment => apartment.DeletedAt == null);
    }
}
