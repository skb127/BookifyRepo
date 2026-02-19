using Bookify.Domain.Abstractions;

namespace Bookify.Domain.Users;

public static class UserErrors
{
    public static Error NotFound { get; } = new(
        "User.NotFound",
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

    public static Error PasswordResetFailed => new(
        "User.PasswordResetFailed",
        "Failed to reset the password, please try again");

    public static Error InvalidToken => new(
        "User.InvalidResetToken",
        "The link is invalid or expired");

    public static Error EmailAlreadyInUse => new(
        "User.EmailAlreadyInUse",
        "The provided email address is already in use");

    public static Error SameEmailProvided => new(
        "User.SameEmailProvided",
        "The new email must be different from the current email");

    public static Error PendingEmailChangeExists => new(
        "User.PendingEmailChangeExists",
        "There is already a pending email change request. Please complete or wait for it to expire");

    public static Error PendingPasswordResetExists => new(
        "User.PendingPasswordResetExists",
        "There is already a pending password reset request. Please check your email or wait for it to expire");

    public static Error InvalidEmailChangeToken => new(
        "User.InvalidEmailChangeToken",
        "The email change token is invalid or has expired");

    public static Error UpdateFailed => new(
        "User.UpdateFailed",
        "Failed to update the user profile");
}
