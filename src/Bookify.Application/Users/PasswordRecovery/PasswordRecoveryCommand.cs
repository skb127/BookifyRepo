using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.PasswordRecovery;

public sealed record PasswordRecoveryCommand(
    string Email) : ICommand;
