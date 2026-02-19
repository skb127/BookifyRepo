using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.InitiateEmailChange;

public sealed record InitiateEmailChangeCommand(
    string NewEmail,
    string CurrentPassword) : ICommand;
