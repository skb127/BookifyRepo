namespace Bookify.Application.Options;

/// <summary>
/// Options for configuring booking behavior, such as checkout session TTL and host approval TTL.
/// </summary>
public sealed class BookingOptions
{
    public double CheckoutSessionTtlMinutes { get; set; } = 30.0;

    public double HostApprovalTtlHours { get; set; } = 24.0;
}
