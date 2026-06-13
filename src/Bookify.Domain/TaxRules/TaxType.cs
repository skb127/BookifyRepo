namespace Bookify.Domain.TaxRules;

public enum TaxType
{
    None = 0,
    Percentage = 1,
    FixedPerNight = 2,
    FixedPerPersonPerNight = 3,
    FixedPerBooking = 4
}
