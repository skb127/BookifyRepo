using Bookify.Functions.Data;
using Bookify.Functions.Options;
using Bookify.Functions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

IHost host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((_, builder) =>
    {
        builder.AddEnvironmentVariables();
        builder.AddUserSecrets<Program>(optional: true);
    })
    .ConfigureServices((context, services) =>
    {
        IConfiguration configuration = context.Configuration;

        services.Configure<DatabaseOptions>(options =>
            options.Database = configuration.GetConnectionString("Database") ?? string.Empty);

        services.Configure<BlobStorageOptions>(options =>
        {
            options.ConnectionString = configuration["BlobStorage:ConnectionString"]
                                       ?? configuration.GetConnectionString("BlobStorage")
                                       ?? string.Empty;
            options.ContainerName = configuration["BlobStorage:ContainerName"] ?? "invoices";
        });

        services.AddScoped<IInvoiceDataService, InvoiceDataService>();
        services.AddSingleton<IPdfGeneratorService, QuestPdfGeneratorService>();
        services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
    })
    .Build();

await host.RunAsync();
