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
        yield return ["test@test.com", "name", "", "123456"];
        yield return ["test@test.com", "name", "lastname", ""];
        yield return ["test@test.com", "name", "lastname", "1"];
        yield return ["test@test.com", "name", "lastname", "12"];
        yield return ["test@test.com", "name", "lastname", "123"];
        yield return ["test@test.com", "name", "lastname", "1234"];
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}