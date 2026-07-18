namespace Bookify.Application.Bookings.GetPriceEstimate;

public sealed record PriceEstimateResponse(
    decimal PriceForPeriodAmount,
    string PriceForPeriodCurrency,
    decimal CleaningFeeAmount,
    string CleaningFeeCurrency,
    decimal AmenitiesUpChargeAmount,
    string AmenitiesUpChargeCurrency,
    decimal ExtraGuestFeeAmount,
    string ExtraGuestFeeCurrency,
    decimal TotalAmount,
    string TotalCurrency,
    int LengthInDays);
