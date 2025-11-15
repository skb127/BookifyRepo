using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users;
public static class UserErrors
{
    // Backing fields kept non-visible to satisfy CA2211 (Non-constant fields should not be visible)
    private static readonly Error s_notFound = new(
        "User.Found",
        "The user with the specified identifier was not found");

    private static readonly Error s_invalidCredentials = new(
        "User.InvalidCredentials",
        "The provided credentials were invalid");

    // Public read-only properties expose the same singleton instances without public static fields
    public static Error NotFound => s_notFound;
    public static Error InvalidCredentials => s_invalidCredentials;
}
