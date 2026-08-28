using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Bookify.Functions.Options;
using Microsoft.Extensions.Options;

namespace Bookify.Functions.Services;

internal sealed class AzureBlobStorageService(IOptions<BlobStorageOptions> options) : IBlobStorageService
{
    private readonly BlobStorageOptions _options = options.Value;

    public async Task<string> UploadAsync(string blobName, byte[] content, string contentType = "application/pdf",
        CancellationToken cancellationToken = default)
    {
        var containerClient = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        using var stream = new MemoryStream(content);
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            }
        };

        await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

        return blobClient.Uri.AbsoluteUri;
    }
}
