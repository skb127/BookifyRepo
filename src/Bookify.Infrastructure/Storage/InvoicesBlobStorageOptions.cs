namespace Bookify.Infrastructure.Storage;

public sealed class InvoicesBlobStorageOptions
{
    public const string SectionName = "InvoicesBlobStorage";

    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "invoices";

    public int SasExpirationMinutes { get; set; } = 10;
}
