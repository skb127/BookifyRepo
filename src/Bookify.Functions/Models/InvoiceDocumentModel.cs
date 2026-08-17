namespace Bookify.Functions.Models;

internal sealed record InvoiceDocumentModel(
    Guid InvoiceId,
    string InvoiceNumber,
    InvoiceType InvoiceType,
    Guid BookingId,
    DateTime IssueDate,
    decimal TotalAmount,
    decimal TaxAmount,
    string Currency,
    string GuestFirstName,
    string GuestLastName,
    string GuestEmail,
    string ApartmentName,
    string ApartmentAddress,
    DateOnly DurationStart,
    DateOnly DurationEnd,
    int TotalNights);
