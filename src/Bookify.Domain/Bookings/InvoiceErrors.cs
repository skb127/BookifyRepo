using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings;

public static class InvoiceErrors
{
    public static Error NotFound { get; } = new(
        "Invoice.NotFound",
        "The invoice with the specified identifier was not found");

    public static Error DownloadUrlGenerationFailed { get; } = new(
        "Invoice.DownloadUrlGenerationFailed",
        "Failed to generate download URL for the invoice");
}
