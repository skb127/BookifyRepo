namespace Bookify.Functions.Models;

internal sealed record TaxLineModel(string TaxName, int TaxType, decimal Rate, decimal Amount);
