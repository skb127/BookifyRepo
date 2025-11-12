namespace Bookify.Domain.Shared;
public record Currency
{
    internal static readonly Currency None = new(""); // We aren't going to return it from the list of all currencies, we don't want to expose it, outside the domain project (internal)
    public static readonly Currency Eur = new("EUR");
    public static readonly Currency Usd = new("USD");

    private Currency(string code) => Code = code;

    public string Code { get; init; }

    public static Currency FromCode(string code) =>
        All.FirstOrDefault(c => c.Equals(code)) ?? 
        throw new ApplicationException("The currency code is invalid");

    public static readonly IReadOnlyCollection<Currency> All = [
        Usd,
        Eur
    ];
}
