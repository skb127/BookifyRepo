namespace Bookify.Application.Bookings.GetBooking;

public sealed record BookingResponse
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Guid ApartmentId { get; init; }

    public int Status { get; init; }

    public int PaymentStatus { get; init; }

    public decimal PriceAmount { get; init; }

    public string PriceCurrency { get; init; } = "";

    public decimal CleaningFeeAmount { get; init; }

    public string CleaningFeeCurrency { get; init; } = "";

    public decimal AmenitiesUpChargeAmount { get; init; }

    public string AmenitiesUpChargeCurrency { get; init; } = "";

    public decimal ExtraGuestChargeAmount { get; init; }

    public string ExtraGuestChargeCurrency { get; init; } = "";

    public decimal TotalPriceAmount { get; init; }

    public string TotalPriceCurrency { get; init; } = "";

    public DateOnly DurationStart { get; init; }

    public DateOnly DurationEnd { get; init; }

    public DateTime CreatedOnUtc { get; init; }

    public int GuestCount { get; init; }

    public IReadOnlyList<BookingTaxResponse> Taxes { get; init; } = [];
}
