namespace Bookify.Application.Abstractions.Storage;

public interface IInvoiceFileService
{
    Uri GenerateDownloadUrl(string blobName);
}
