using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Bookings;

public sealed class Invoice : Entity
{
    private Invoice(
        Guid id,
        Guid bookingId,
        InvoiceNumber invoiceNumber,
        InvoiceType invoiceType,
        Guid? originalInvoiceId,
        InvoiceStatus status,
        DateTime issueDate,
        decimal totalAmount,
        decimal taxAmount,
        string currency,
        DateTime createdOnUtc)
        : base(id)
    {
        BookingId = bookingId;
        InvoiceNumber = invoiceNumber;
        InvoiceType = invoiceType;
        OriginalInvoiceId = originalInvoiceId;
        Status = status;
        IssueDate = issueDate;
        TotalAmount = totalAmount;
        TaxAmount = taxAmount;
        Currency = currency;
        CreatedOnUtc = createdOnUtc;
    }

    private Invoice()
    {
    }

    public Guid BookingId { get; private set; }
    public InvoiceNumber InvoiceNumber { get; private set; } = null!;
    public InvoiceType InvoiceType { get; private set; }
    public Guid? OriginalInvoiceId { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public DateTime IssueDate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string? PdfUrl { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }

    public static Invoice CreateForBooking(
        Guid bookingId,
        decimal totalAmount,
        decimal taxAmount,
        string currency,
        DateTime utcNow) =>
        new(
            Guid.CreateVersion7(),
            bookingId,
            InvoiceNumber.Create(bookingId),
            InvoiceType.Invoice,
            originalInvoiceId: null,
            InvoiceStatus.Pending,
            issueDate: utcNow,
            totalAmount,
            taxAmount,
            currency,
            createdOnUtc: utcNow);

    public static Invoice CreateCreditNote(
        Guid bookingId,
        Guid originalInvoiceId,
        decimal totalAmount,
        decimal taxAmount,
        string currency,
        DateTime utcNow) =>
        new(
            Guid.CreateVersion7(),
            bookingId,
            InvoiceNumber.CreateCreditNote(bookingId),
            InvoiceType.CreditNote,
            originalInvoiceId,
            status: InvoiceStatus.Pending,
            issueDate: utcNow,
            totalAmount,
            taxAmount,
            currency,
            createdOnUtc: utcNow);

    public void MarkAsGenerated(string pdfUrl)
    {
        Status = InvoiceStatus.Generated;
        PdfUrl = pdfUrl;
    }

    public void MarkAsError() => Status = InvoiceStatus.Error;
}
