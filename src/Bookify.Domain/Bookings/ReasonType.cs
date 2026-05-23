namespace Bookify.Domain.Bookings;

/// <summary>
/// Represents the type of reason for a booking state transition or cancellation.
/// </summary>
public enum ReasonType
{
    None = 0,
    EarlyDeparture = 1,
    Maintenance = 2,
    GuestRequest = 3,
    HostDecision = 4,
    Other = 5
}
