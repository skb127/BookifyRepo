namespace Bookify.Functions.Services;

internal interface IBlobStorageService
{
    Task<string> UploadAsync(string blobName, byte[] content, string contentType = "application/pdf", CancellationToken cancellationToken = default);
}
