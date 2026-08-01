using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.UnbanUser;

public sealed record UnbanUserCommand(Guid UserId) : ICommand;
