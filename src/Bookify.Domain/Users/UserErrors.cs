using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users;
public static class UserErrors
{
    public static Error NotFound { get; } = new(
        "User.Found",
        "The user with the specified identifier was not found");

    public static Error InvalidCredentials { get; } = new(
        "User.InvalidCredentials",
        "The provided credentials were invalid");
}
