using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.ChangeUserPassword;

public sealed record ChangeUserPasswordCommand(
    string CurrentPassword,
    string NewPassword) : ICommand;
