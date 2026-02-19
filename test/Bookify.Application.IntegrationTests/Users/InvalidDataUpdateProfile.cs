using System.Collections;
using System.Globalization;

namespace Bookify.Application.IntegrationTests.Users;

public sealed class InvalidDataUpdateProfile : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        // Empty FirstName
        yield return ["", "LastName", null!, "2000-01-01"];
        // Empty LastName
        yield return ["FirstName", "", null!, "2000-01-01"];
        // Invalid phone number format (not E.164)
        yield return ["FirstName", "LastName", "invalid-phone", "2000-01-01"];
        // Under 18 years old (born today)
        yield return ["FirstName", "LastName", null!, DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)];
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
