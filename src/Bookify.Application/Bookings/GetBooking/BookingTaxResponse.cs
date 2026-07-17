namespace Bookify.Application.Bookings.GetBooking;

public sealed class BookingTaxResponse
{
    public string TaxRuleName { get; init; } = "";
    public decimal CalculatedAmount { get; init; }
    public string Currency { get; init; } = "";
}
