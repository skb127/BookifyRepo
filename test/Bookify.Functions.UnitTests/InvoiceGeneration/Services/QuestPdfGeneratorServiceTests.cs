using System.Text;
using Bookify.Functions.Models;
using Bookify.Functions.Services;
using FluentAssertions;
using QuestPDF.Infrastructure;

namespace Bookify.Functions.UnitTests.InvoiceGeneration.Services;

public class QuestPdfGeneratorServiceTests
{
    private readonly QuestPdfGeneratorService _service;

    public QuestPdfGeneratorServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        _service = new QuestPdfGeneratorService();
    }

    private static InvoiceDocumentModel CreateSampleModel(InvoiceType invoiceType = InvoiceType.Invoice)
    {
        return new InvoiceDocumentModel(
            Guid.NewGuid(),
            "INV-2026-9999",
            invoiceType,
            Guid.NewGuid(),
            DateTime.UtcNow,
            250.00m,
            25.00m,
            "EUR",
            "Jane",
            "Smith",
            "jane.smith@example.com",
            "Beachfront Villa",
            "456 Ocean Drive, Barcelona, Spain",
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 15),
            5);
    }

    [Fact]
    public void Generate_ShouldReturnNonEmptyBytes_ForInvoice()
    {
        // Arrange
        InvoiceDocumentModel model = CreateSampleModel();

        // Act
        byte[] pdfBytes = _service.Generate(model);

        // Assert
        pdfBytes.Should().NotBeNull();
        pdfBytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_ShouldReturnNonEmptyBytes_ForCreditNote()
    {
        // Arrange
        InvoiceDocumentModel model = CreateSampleModel(InvoiceType.CreditNote);

        // Act
        byte[] pdfBytes = _service.Generate(model);

        // Assert
        pdfBytes.Should().NotBeNull();
        pdfBytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_ShouldContainValidPdfHeader()
    {
        // Arrange
        InvoiceDocumentModel model = CreateSampleModel();

        // Act
        byte[] pdfBytes = _service.Generate(model);

        // Assert
        string header = Encoding.ASCII.GetString(pdfBytes, 0, 5);
        header.Should().Be("%PDF-");
    }
}
