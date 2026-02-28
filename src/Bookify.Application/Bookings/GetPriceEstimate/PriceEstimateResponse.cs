namespace Bookify.Application.Bookings.GetPriceEstimate;

public sealed record PriceEstimateResponse(
    decimal PriceForPeriodAmount,
    string PriceForPeriodCurrency,
    decimal CleaningFeeAmount,
    string CleaningFeeCurrency,
    decimal AmenitiesUpChargeAmount,
    string AmenitiesUpChargeCurrency,
    decimal TotalAmount,
    string TotalCurrency,
    int LengthInDays);
