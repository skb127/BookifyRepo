using Bookify.Functions.Models;

namespace Bookify.Functions.Services;

internal interface IPdfGeneratorService
{
    byte[] Generate(InvoiceDocumentData data);
}
