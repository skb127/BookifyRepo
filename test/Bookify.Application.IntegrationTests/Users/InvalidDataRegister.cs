using System.Collections;

namespace Bookify.Application.IntegrationTests.Users;

public sealed class InvalidDataRegister : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return ["", "name", "lastname", "123456"];
        yield return ["test.com", "name", "lastname", "123456"];
        yield return ["@test.com", "name", "lastname", "123456"];
        yield return ["test@", "name", "lastname", "123456"];
        yield return ["test@test.com", "", "lastname", "123456"];
        yield return ["test@test.com", "name", "", "1234aB?"];
        yield return ["test@test.com", "name", "lastname", ""];
        yield return ["test@test.com", "name", "lastname", "1"];
        yield return ["test@test.com", "name", "lastname", "12345_AB"];
        yield return ["test@test.com", "name", "lastname", "123456AB"];
        yield return ["test@test.com", "name", "lastname", "1234_56?"];
        yield return ["test@test.com", "name", "lastname", "1234"];
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
