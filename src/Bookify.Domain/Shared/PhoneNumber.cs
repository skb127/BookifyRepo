using System.Text.RegularExpressions;

namespace Bookify.Domain.Shared;

public partial record PhoneNumber
{
    private const string Pattern = @"^\+?[1-9]\d{1,14}$"; // E.164 format

    private PhoneNumber(string value) => Value = value;

    public string Value { get; init; }

    public static PhoneNumber? Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!MyRegex().IsMatch(value))
        {
            throw new InvalidOperationException("Phone number format is invalid");
        }

        return new PhoneNumber(value);
    }

    [GeneratedRegex(Pattern)]
    private static partial Regex MyRegex();
}
