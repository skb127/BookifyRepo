using Bookify.Domain.Bookings;

namespace Bookify.Api.Controllers.Bookings;

/// <summary>
/// Represents the request payload for providing an optional booking reason.
/// </summary>
public sealed record BookingReasonRequest(ReasonType? Type, string? Description);
