using System.Collections;

namespace Bookify.Application.IntegrationTests.Apartments;

public sealed class InvalidDataCreateApartment : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        // 1. Empty Name
        yield return ["", "Description of Apartment 1", "Country", "State", "ZipCode", "City", "Street", 100.0m, "USD", 50.0m, "USD", Array.Empty<int>()];

        // 2. Negative Price Amount
        yield return ["Apartment 1", "Description of Apartment 1", "Country", "State", "ZipCode", "City", "Street", -10.0m, "USD", 50.0m, "USD", Array.Empty<int>()];

        // 3. Invalid Currency
        yield return ["Apartment 1", "Description of Apartment 1", "Country", "State", "ZipCode", "City", "Street", 100.0m, "XYZ", 50.0m, "USD", Array.Empty<int>()];

        // 4. Negative Cleaning Fee Amount
        yield return ["Apartment 1", "Description of Apartment 1", "Country", "State", "ZipCode", "City", "Street", 100.0m, "USD", -5.0m, "USD", Array.Empty<int>()];
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
