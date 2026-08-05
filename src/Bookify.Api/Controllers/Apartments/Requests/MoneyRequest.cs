using System.Text.Json.Serialization;

namespace Bookify.Api.Controllers.Apartments.Requests;

public sealed record MoneyRequest([property: JsonRequired] decimal Amount, string Currency);
