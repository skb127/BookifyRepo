using System.Text.Json;
using Bookify.Functions.Data;
using Bookify.Functions.Functions;
using Bookify.Functions.Models;
using Bookify.Functions.Services;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Functions.UnitTests.InvoiceGeneration.Functions;

public class GenerateInvoiceFunctionTests
{
    private readonly IInvoiceDataService _invoiceDataServiceMock;
    private readonly IPdfGeneratorService _pdfGeneratorServiceMock;
    private readonly IBlobStorageService _blobStorageServiceMock;
    private readonly ILogger<GenerateInvoiceFunction> _loggerMock;
    private readonly FunctionContext _contextMock;
    private readonly GenerateInvoiceFunction _function;

    public GenerateInvoiceFunctionTests()
    {
        _invoiceDataServiceMock = Substitute.For<IInvoiceDataService>();
        _pdfGeneratorServiceMock = Substitute.For<IPdfGeneratorService>();
        _blobStorageServiceMock = Substitute.For<IBlobStorageService>();
        _loggerMock = Substitute.For<ILogger<GenerateInvoiceFunction>>();
        _contextMock = Substitute.For<FunctionContext>();
        _contextMock.CancellationToken.Returns(CancellationToken.None);

        _function = new GenerateInvoiceFunction(
            _invoiceDataServiceMock,
            _pdfGeneratorServiceMock,
            _blobStorageServiceMock,
            _loggerMock);
    }

    private static InvoiceDocumentModel CreateSampleDocumentModel(Guid invoiceId, InvoiceType invoiceType = InvoiceType.Invoice)
    {
        return new InvoiceDocumentModel(
            invoiceId,
            "INV-2026-0001",
            invoiceType,
            Guid.NewGuid(),
            DateTime.UtcNow,
            200.00m,
            20.00m,
            "EUR",
            "John",
            "Doe",
            "john.doe@example.com",
            "Luxury Suite",
            "123 Main St, Madrid, Spain",
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 5),
            4);
    }

    [Fact]
    public async Task Run_ShouldGeneratePdf_AndUploadToBlob_AndUpdateDb()
    {
        // Arrange
        Guid invoiceId = Guid.NewGuid();
        var message = new InvoiceRequestMessage
        {
            InvoiceId = invoiceId,
            BookingId = Guid.NewGuid(),
            InvoiceType = InvoiceType.Invoice
        };
        string messageBody = JsonSerializer.Serialize(message);
        InvoiceDocumentModel documentModel = CreateSampleDocumentModel(invoiceId);
        byte[] pdfBytes = [1, 2, 3, 4, 5];
        const string expectedBlobUrl = "https://storage.blob.core.windows.net/invoices/invoice_INV-2026-0001.pdf";

        _invoiceDataServiceMock.GetInvoiceDocumentDataAsync(invoiceId, Arg.Any<CancellationToken>())
            .Returns(documentModel);

        _pdfGeneratorServiceMock.Generate(documentModel)
            .Returns(pdfBytes);

        _blobStorageServiceMock.UploadAsync("invoice_INV-2026-0001.pdf", pdfBytes, "application/pdf",
                Arg.Any<CancellationToken>())
            .Returns(expectedBlobUrl);

        // Act
        await _function.Run(messageBody, _contextMock);

        // Assert
        await _invoiceDataServiceMock.Received(1).GetInvoiceDocumentDataAsync(invoiceId, Arg.Any<CancellationToken>());
        _pdfGeneratorServiceMock.Received(1).Generate(documentModel);
        await _blobStorageServiceMock.Received(1).UploadAsync("invoice_INV-2026-0001.pdf", pdfBytes, "application/pdf",
            Arg.Any<CancellationToken>());
        await _invoiceDataServiceMock.Received(1)
            .MarkInvoiceAsGeneratedAsync(invoiceId, expectedBlobUrl, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_ShouldUploadPdf_WithCorrectContentType()
    {
        // Arrange
        Guid invoiceId = Guid.NewGuid();
        var message = new InvoiceRequestMessage
        {
            InvoiceId = invoiceId,
            BookingId = Guid.NewGuid(),
            InvoiceType = InvoiceType.CreditNote
        };
        string messageBody = JsonSerializer.Serialize(message);
        InvoiceDocumentModel documentModel = CreateSampleDocumentModel(invoiceId, InvoiceType.CreditNote);
        byte[] pdfBytes = [10, 20, 30];
        const string expectedBlobUrl = "https://storage.blob.core.windows.net/invoices/creditnote_INV-2026-0001.pdf";

        _invoiceDataServiceMock.GetInvoiceDocumentDataAsync(invoiceId, Arg.Any<CancellationToken>())
            .Returns(documentModel);
        _pdfGeneratorServiceMock.Generate(documentModel).Returns(pdfBytes);
        _blobStorageServiceMock.UploadAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedBlobUrl);

        // Act
        await _function.Run(messageBody, _contextMock);

        // Assert
        await _blobStorageServiceMock.Received(1).UploadAsync(
            "creditnote_INV-2026-0001.pdf",
            pdfBytes,
            "application/pdf",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_ShouldLogWarning_WhenInvoiceDataNotFound()
    {
        // Arrange
        Guid invoiceId = Guid.NewGuid();
        var message = new InvoiceRequestMessage
            { InvoiceId = invoiceId, BookingId = Guid.NewGuid(), InvoiceType = InvoiceType.Invoice };
        string messageBody = JsonSerializer.Serialize(message);

        _invoiceDataServiceMock.GetInvoiceDocumentDataAsync(invoiceId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        await _function.Run(messageBody, _contextMock);

        // Assert
        _pdfGeneratorServiceMock.DidNotReceiveWithAnyArgs().Generate(null!);
        await _blobStorageServiceMock.DidNotReceiveWithAnyArgs()
            .UploadAsync(null!, null!, null!, CancellationToken.None);
        await _invoiceDataServiceMock.DidNotReceiveWithAnyArgs()
            .MarkInvoiceAsGeneratedAsync(Guid.Empty, null!, CancellationToken.None);
    }

    [Fact]
    public async Task Run_ShouldNotUploadBlob_WhenPdfGenerationFails()
    {
        // Arrange
        Guid invoiceId = Guid.NewGuid();
        var message = new InvoiceRequestMessage
            { InvoiceId = invoiceId, BookingId = Guid.NewGuid(), InvoiceType = InvoiceType.Invoice };
        string messageBody = JsonSerializer.Serialize(message);
        InvoiceDocumentModel documentModel = CreateSampleDocumentModel(invoiceId);

        _invoiceDataServiceMock.GetInvoiceDocumentDataAsync(invoiceId, Arg.Any<CancellationToken>())
            .Returns(documentModel);

        _pdfGeneratorServiceMock.Generate(documentModel)
            .Throws(new InvalidOperationException("PDF generation failed"));

        // Act
        Func<Task> act = async () => await _function.Run(messageBody, _contextMock);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _blobStorageServiceMock.DidNotReceiveWithAnyArgs()
            .UploadAsync(null!, null!, null!, CancellationToken.None);
        await _invoiceDataServiceMock.Received(1).MarkInvoiceAsErrorAsync(invoiceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_ShouldMarkInvoiceAsError_WhenBlobUploadFails()
    {
        // Arrange
        Guid invoiceId = Guid.NewGuid();
        var message = new InvoiceRequestMessage
            { InvoiceId = invoiceId, BookingId = Guid.NewGuid(), InvoiceType = InvoiceType.Invoice };
        string messageBody = JsonSerializer.Serialize(message);
        InvoiceDocumentModel documentModel = CreateSampleDocumentModel(invoiceId);
        byte[] pdfBytes = [1, 2, 3];

        _invoiceDataServiceMock.GetInvoiceDocumentDataAsync(invoiceId, Arg.Any<CancellationToken>())
            .Returns(documentModel);
        _pdfGeneratorServiceMock.Generate(documentModel).Returns(pdfBytes);
        _blobStorageServiceMock.UploadAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Blob upload failed"));

        // Act
        Func<Task> act = async () => await _function.Run(messageBody, _contextMock);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _invoiceDataServiceMock.Received(1).MarkInvoiceAsErrorAsync(invoiceId, Arg.Any<CancellationToken>());
        await _invoiceDataServiceMock.DidNotReceiveWithAnyArgs()
            .MarkInvoiceAsGeneratedAsync(Guid.Empty, null!, CancellationToken.None);
    }

    [Fact]
    public async Task Run_ShouldLogWarning_WhenJsonMessageIsInvalid()
    {
        // Arrange
        const string invalidJson = "{ invalid_json }";

        // Act
        await _function.Run(invalidJson, _contextMock);

        // Assert
        await _invoiceDataServiceMock.DidNotReceiveWithAnyArgs()
            .GetInvoiceDocumentDataAsync(Guid.Empty, CancellationToken.None);
    }
}
