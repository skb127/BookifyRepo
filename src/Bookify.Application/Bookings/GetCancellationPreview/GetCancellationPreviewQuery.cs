using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Bookings.GetCancellationPreview;

public record GetCancellationPreviewQuery(Guid BookingId) : IQuery<CancellationPreviewResponse>;
