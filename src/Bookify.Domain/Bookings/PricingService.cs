using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;

namespace Bookify.Domain.Bookings;

public class PricingService
{
    public PricingDetails CalculatePrice(Apartment apartment, DateRange period, int guestCount = 1)
    {
        Currency currency = apartment.Price.Currency;

        var priceForPeriod = new Money(
            apartment.Price.Amount * period.LengthInDays,
            currency);

        decimal percentageUpCharge = 0m;
        foreach (Amenity amenity in apartment.Amenities)
        {
            percentageUpCharge += amenity switch
            {
                Amenity.GardenView or Amenity.MountainView => 0.05m,
                Amenity.Spa => 0.05m,
                Amenity.SwimmingPool => 0.04m,
                Amenity.PetFriendly => 0.03m,
                Amenity.Gym or Amenity.Terrace => 0.02m,
                Amenity.AirConditioning or Amenity.Parking or Amenity.WiFi => 0.01m,
                _ => 0m
            };
        }

        var amenitiesUpCharge = Money.Zero(currency);
        if (percentageUpCharge > 0)
        {
            amenitiesUpCharge = new Money(
                priceForPeriod.Amount * percentageUpCharge,
                currency);
        }

        int extraGuests = Math.Max(0, guestCount - apartment.BaseGuests);
        Money extraGuestCharge = extraGuests > 0
            ? new Money(apartment.ExtraGuestFee.Amount * extraGuests * period.LengthInDays, currency)
            : Money.Zero(currency);

        var totalPrice = Money.Zero(currency);

        totalPrice += priceForPeriod;

        if (!apartment.CleaningFee.IsZero())
        {
            totalPrice += apartment.CleaningFee;
        }

        totalPrice += amenitiesUpCharge;
        totalPrice += extraGuestCharge;

        return new PricingDetails(priceForPeriod, apartment.CleaningFee, amenitiesUpCharge, extraGuestCharge, totalPrice);
    }
}
