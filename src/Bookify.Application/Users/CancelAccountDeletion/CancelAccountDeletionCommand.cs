using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.CancelAccountDeletion;

public sealed record CancelAccountDeletionCommand(string Token) : ICommand;
