namespace Bookify.Functions.Models;

internal sealed record InvoiceDocumentData(
    InvoiceDocumentModel Document,
    IReadOnlyList<TaxLineModel> TaxLines);
