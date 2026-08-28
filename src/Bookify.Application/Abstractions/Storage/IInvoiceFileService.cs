using Bookify.Domain.Abstractions;

namespace Bookify.Application.Abstractions.Storage;

public interface IInvoiceFileService
{
    Result<Uri> GenerateDownloadUrl(string blobName);
}
