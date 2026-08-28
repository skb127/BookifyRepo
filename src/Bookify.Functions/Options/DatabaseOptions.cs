namespace Bookify.Functions.Options;

internal sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    public string Database { get; set; } = string.Empty;
}
