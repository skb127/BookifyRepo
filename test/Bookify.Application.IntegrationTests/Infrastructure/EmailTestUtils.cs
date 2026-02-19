using System.Text.RegularExpressions;

namespace Bookify.Application.IntegrationTests.Infrastructure;

public static class EmailTestUtils
{
    public static string ExtractToken(string emailBody, string endpoint)
    {
        ArgumentNullException.ThrowIfNull(emailBody);

        // Build the regex dynamically to support different endpoints (e.g confirm-email-change, reset-password)
        var regex = new Regex($@"{endpoint}/([a-zA-Z0-9\-_=+/%.]+)", RegexOptions.None, TimeSpan.FromMilliseconds(1000));
        Match match = regex.Match(emailBody);

        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException(
                $"Could not extract token for endpoint '{endpoint}' from email body. Body preview: {emailBody[..Math.Min(200, emailBody.Length)]}");
    }
}