using System.Text.Json;
using Bookify.Functions.Data;
using Bookify.Functions.Models;
using Bookify.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bookify.Functions.Functions;

internal sealed class GenerateInvoiceFunction
{
    private readonly IInvoiceDataService _invoiceDataService;
    private readonly IPdfGeneratorService _pdfGeneratorService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<GenerateInvoiceFunction> _logger;

    public GenerateInvoiceFunction(
        IInvoiceDataService invoiceDataService,
        IPdfGeneratorService pdfGeneratorService,
        IBlobStorageService blobStorageService,
        ILogger<GenerateInvoiceFunction> logger)
    {
        _invoiceDataService = invoiceDataService;
        _pdfGeneratorService = pdfGeneratorService;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    [Function("GenerateInvoiceFunction")]
    public async Task Run(
        [ServiceBusTrigger("invoice-requests", Connection = "ServiceBusConnection")] string messageBody,
        FunctionContext context)
    {
        _logger.LogInformation("Processing invoice request: {MessageBody}", messageBody);

        InvoiceRequestMessage? request;
        try
        {
            request = JsonSerializer.Deserialize<InvoiceRequestMessage>(messageBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize invoice request message.");
            return;
        }

        if (request is null || request.InvoiceId == Guid.Empty)
        {
            _logger.LogWarning("Invalid invoice request message content.");
            return;
        }

        InvoiceDocumentData? documentData = await _invoiceDataService.GetInvoiceDocumentDataAsync(request.InvoiceId, context.CancellationToken);
        if (documentData is null)
        {
            _logger.LogWarning("Invoice document data not found for InvoiceId {InvoiceId}. Throwing exception to trigger retry.", request.InvoiceId);
            throw new InvalidOperationException($"Invoice document data not found for InvoiceId {request.InvoiceId}.");
        }

        try
        {
            byte[] pdfBytes = _pdfGeneratorService.Generate(documentData);

            string blobName = $"{documentData.Document.InvoiceType.ToString().ToLowerInvariant()}_{documentData.Document.InvoiceNumber}.pdf";
            await _blobStorageService.UploadAsync(blobName, pdfBytes, "application/pdf", context.CancellationToken);

            await _invoiceDataService.MarkInvoiceAsGeneratedAsync(request.InvoiceId, blobName, context.CancellationToken);
            _logger.LogInformation("Successfully generated invoice {InvoiceNumber} with blob name {BlobName}.", documentData.Document.InvoiceNumber, blobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing invoice {InvoiceId}. Marking invoice status as Error.", request.InvoiceId);
            await _invoiceDataService.MarkInvoiceAsErrorAsync(request.InvoiceId, context.CancellationToken);
            throw;
        }
    }
}
