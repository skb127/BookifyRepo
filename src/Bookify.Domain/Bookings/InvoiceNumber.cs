namespace Bookify.Domain.Bookings;

public record InvoiceNumber
{
    private InvoiceNumber(string value) => Value = value;

    public string Value { get; init; }

    public static InvoiceNumber Create(Guid bookingId)
        => new($"INV-{bookingId:N}".ToUpperInvariant());

    public static InvoiceNumber CreateCreditNote(Guid bookingId)
        => new($"CN-{bookingId:N}".ToUpperInvariant());

    public override string ToString() => Value;
}
