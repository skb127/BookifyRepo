using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.FinalizeAccountDeletion;

public sealed record FinalizeAccountDeletionCommand(Guid UserId) : ICommand;
