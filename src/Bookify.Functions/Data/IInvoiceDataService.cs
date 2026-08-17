using System.Diagnostics.CodeAnalysis;
using Bookify.Functions.Models;

namespace Bookify.Functions.Data;

internal interface IInvoiceDataService
{
    Task<InvoiceDocumentModel?> GetInvoiceDocumentDataAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    [SuppressMessage("Design", "CA1054:Uri parameters should not be strings", Justification = "PDF URLs are stored as strings in the database and handled as strings.")]
    Task MarkInvoiceAsGeneratedAsync(Guid invoiceId, string pdfUrl, CancellationToken cancellationToken = default);

    Task MarkInvoiceAsErrorAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
