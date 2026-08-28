using Bookify.Application.Abstractions.Caching;

namespace Bookify.Application.Bookings.DownloadInvoice;

public sealed record DownloadInvoiceQuery(Guid BookingId, Guid InvoiceId) : ICachedQuery<Uri>
{
    public string CacheKey => CacheKeys.InvoiceSAS(InvoiceId);
    public TimeSpan? Expiration => TimeSpan.FromMinutes(10);
}
