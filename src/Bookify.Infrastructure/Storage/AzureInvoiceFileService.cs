using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Bookify.Application.Abstractions.Storage;
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

    public Uri GenerateDownloadUrl(string blobName)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(blobName);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "BlobClient cannot generate SAS URI. Please ensure connection string contains shared key credentials.");
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

        return blobClient.GenerateSasUri(sasBuilder);
    }
}
