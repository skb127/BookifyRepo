using Bookify.Functions.Models;

namespace Bookify.Functions.Data;

internal interface IInvoiceDataService
{
    Task<InvoiceDocumentData?> GetInvoiceDocumentDataAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    Task MarkInvoiceAsGeneratedAsync(Guid invoiceId, string pdfBlobName, CancellationToken cancellationToken = default);

    Task MarkInvoiceAsErrorAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
