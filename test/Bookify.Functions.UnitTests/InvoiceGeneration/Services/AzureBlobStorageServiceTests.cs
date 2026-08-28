using Bookify.Functions.Options;
using Bookify.Functions.Services;
using FluentAssertions;

namespace Bookify.Functions.UnitTests.InvoiceGeneration.Services;

public class AzureBlobStorageServiceTests
{
    [Fact]
    public async Task UploadAsync_ShouldThrow_WhenConnectionStringIsInvalid()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new BlobStorageOptions
        {
            ConnectionString = "InvalidConnectionString",
            ContainerName = "test-container"
        });

        var service = new AzureBlobStorageService(options);
        byte[] content = [1, 2, 3];

        // Act
        Func<Task> act = async () => await service.UploadAsync("test.pdf", content);

        // Assert
        await act.Should().ThrowAsync<FormatException>();
    }
}
