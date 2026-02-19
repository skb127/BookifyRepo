using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.PasswordReset;

public sealed record PasswordResetCommand(
    string Token,
    string NewPassword) : ICommand;
