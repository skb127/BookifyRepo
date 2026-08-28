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

    private static InvoiceDocumentModel CreateSampleModel(
        InvoiceType invoiceType = InvoiceType.Invoice,
        decimal? refundAmount = null,
        string? refundReason = null) =>
        new(
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
            5,
            PriceForPeriod: 200.00m,
            CleaningFee: 30.00m,
            AmenitiesUpCharge: 10.00m,
            ExtraGuestCharge: 10.00m,
            GuestCount: 6,
            BaseGuests: 1,
            ExtraGuestFee: 2.00m,
            OriginalInvoiceNumber: invoiceType == InvoiceType.CreditNote ? "INV-2026-9999" : null,
            OriginalTotalAmount: invoiceType == InvoiceType.CreditNote ? 250.00m : null,
            OriginalTaxAmount: invoiceType == InvoiceType.CreditNote ? 25.00m : null,
            RefundAmount: refundAmount,
            RefundReason: refundReason);

    [Fact]
    public void Generate_ShouldReturnNonEmptyBytes_ForInvoice()
    {
        // Arrange
        InvoiceDocumentModel model = CreateSampleModel();
        var data = new InvoiceDocumentData(model, []);

        // Act
        byte[] pdfBytes = _service.Generate(data);

        // Assert
        pdfBytes.Should().NotBeNull();
        pdfBytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_ShouldReturnNonEmptyBytes_ForCreditNote()
    {
        // Arrange
        InvoiceDocumentModel model = CreateSampleModel(InvoiceType.CreditNote);
        var data = new InvoiceDocumentData(model, []);

        // Act
        byte[] pdfBytes = _service.Generate(data);

        // Assert
        pdfBytes.Should().NotBeNull();
        pdfBytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_ShouldContainValidPdfHeader()
    {
        // Arrange
        InvoiceDocumentModel model = CreateSampleModel();
        var data = new InvoiceDocumentData(model, []);

        // Act
        byte[] pdfBytes = _service.Generate(data);

        // Assert
        string header = Encoding.ASCII.GetString(pdfBytes, 0, 5);
        header.Should().Be("%PDF-");
    }

    [Fact]
    public void Generate_ShouldReturnValidPdf_ForCreditNoteWithPartialRefund()
    {
        // Arrange
        InvoiceDocumentModel model = CreateSampleModel(
            InvoiceType.CreditNote,
            refundAmount: 200.00m,
            refundReason: "Cancelled by Guest");
        var taxLines = new List<TaxLineModel>
        {
            new("VAT", 0, 0.21m, 42.00m)
        };
        var data = new InvoiceDocumentData(model, taxLines);

        // Act
        byte[] pdfBytes = _service.Generate(data);

        // Assert
        pdfBytes.Should().NotBeNull();
        pdfBytes.Length.Should().BeGreaterThan(0);
        string header = Encoding.ASCII.GetString(pdfBytes, 0, 5);
        header.Should().Be("%PDF-");
    }

    [Fact]
    public void Generate_ShouldReturnValidPdf_ForInvoiceWithTaxLines()
    {
        // Arrange
        InvoiceDocumentModel model = CreateSampleModel();
        var taxLines = new List<TaxLineModel>
        {
            new("VAT", 0, 0.21m, 42.00m),
            new("Tourist Tax", 1, 2.50m, 12.50m)
        };
        var data = new InvoiceDocumentData(model, taxLines);

        // Act
        byte[] pdfBytes = _service.Generate(data);

        // Assert
        pdfBytes.Should().NotBeNull();
        pdfBytes.Length.Should().BeGreaterThan(0);
        string header = Encoding.ASCII.GetString(pdfBytes, 0, 5);
        header.Should().Be("%PDF-");
    }
}
