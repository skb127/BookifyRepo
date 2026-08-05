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

    public static Error CannotDeleteAdmin => new(
        "User.CannotDeleteAdmin",
        "Admins cannot be deleted via self deactivation");

    public static Error CannotDeleteLastAdmin => new(
        "User.CannotDeleteLastAdmin",
        "The last admin account cannot be deleted");

    public static Error CannotBanAdmin => new(
        "User.CannotBanAdmin",
        "Admin users cannot be banned");

    public static Error HasActiveBookingsAsGuest => new(
        "User.HasActiveBookingsAsGuest",
        "User has active bookings as a guest and cannot be deleted");

    public static Error HasActiveBookingsAsHost => new(
        "User.HasActiveBookingsAsHost",
        "Host has active bookings for their apartments and cannot be deleted");

    public static Error AlreadyDeleted => new(
        "User.AlreadyDeleted",
        "The user is already deleted");

    public static Error NotPendingDeletion => new(
        "User.NotPendingDeletion",
        "The user is not pending deletion");

    public static Error AlreadySuspended => new(
        "User.AlreadySuspended",
        "The user is already banned/suspended");

    public static Error NotSuspended => new(
        "User.NotSuspended",
        "The user is not suspended");

    public static Error RequestFailed => new(
        "User.RequestFailed",
        "The request could not be completed");

    public static Error DeletionWindowExpired => new(
        "User.DeletionWindowExpired",
        "The deletion cancellation window has expired");
}
