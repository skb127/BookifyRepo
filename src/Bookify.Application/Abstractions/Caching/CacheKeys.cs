namespace Bookify.Application.Abstractions.Caching;

public static class CacheKeys
{
    public static string Apartment(Guid id) => $"apartments-{id}";
    public static string User(Guid id) => $"users-{id}";
    public static string InvoiceSAS(Guid invoiceId) => $"invoice-sas-{invoiceId}";
}
