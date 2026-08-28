using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Bookify.Application.Abstractions.Storage;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Microsoft.Extensions.Options;

namespace Bookify.Infrastructure.Storage;

internal sealed class AzureInvoiceFileService : IInvoiceFileService
{
    private readonly BlobContainerClient _containerClient;
    private readonly InvoicesBlobStorageOptions _options;

    public AzureInvoiceFileService(IOptions<InvoicesBlobStorageOptions> options)
    {
        _options = options.Value;
        _containerClient = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
    }

    public Result<Uri> GenerateDownloadUrl(string blobName)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(blobName);

        if (!blobClient.CanGenerateSasUri)
        {
            return Result.Failure<Uri>(InvoiceErrors.DownloadUrlGenerationFailed);
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _options.ContainerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(_options.SasExpirationMinutes),
            Protocol = SasProtocol.HttpsAndHttp
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return Result.Success(blobClient.GenerateSasUri(sasBuilder));
    }
}
