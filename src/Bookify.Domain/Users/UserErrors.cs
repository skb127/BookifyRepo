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
    
    public static Error AlreadyExists { get; } = new(
        "User.AlreadyExists",
        "The user with the specified email already exists");

    public static Error InvalidCurrentCredentials { get; } = new(
        "User.InvalidCurrentCredentials",
        "The current password provided is invalid");

    public static Error PasswordChangeFailed => new(
        "User.PasswordChangeFailed",
        "Failed to change the password, please try again later");
}
