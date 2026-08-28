using System.Globalization;

namespace Bookify.Functions.Services;

internal static class CurrencyFormatter
{
    public static string Format(decimal amount, string currency) =>
        currency.ToUpperInvariant() switch
        {
            "EUR" => $"{amount.ToString("N2", new CultureInfo("es-ES"))} €",
            "USD" => $"${amount.ToString("N2", CultureInfo.InvariantCulture)}",
            "GBP" => $"£{amount.ToString("N2", CultureInfo.InvariantCulture)}",
            _ => $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}"
        };
}
