using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.ChangePasswordUser;

public sealed record ChangePasswordUserCommand(
    string CurrentPassword,
    string NewPassword) : ICommand;
