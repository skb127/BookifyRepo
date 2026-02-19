using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.ConfirmEmailChange;

public sealed record ConfirmEmailChangeCommand(string Token) : ICommand;
