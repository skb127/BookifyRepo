namespace Bookify.Application.Options;

/// <summary>
/// Options for configuring booking behavior, such as the courtesy block period.
/// </summary>
public sealed class BookingOptions
{
    public int CourtesyBlockHours { get; set; } = 24;
}
