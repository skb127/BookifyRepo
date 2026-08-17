using System.Text.Json.Serialization;

namespace Bookify.Functions.Models;

internal sealed class InvoiceRequestMessage
{
    public Guid InvoiceId { get; set; }
    public Guid BookingId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InvoiceType InvoiceType { get; set; }
}
