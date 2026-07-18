using Bookify.Domain.Shared;

namespace Bookify.Domain.Bookings;

public record PricingDetails(
    Money PriceForPeriod,
    Money CleaningFee,
    Money AmenitiesUpCharge,
    Money ExtraGuestCharge,
    Money TotalPrice);