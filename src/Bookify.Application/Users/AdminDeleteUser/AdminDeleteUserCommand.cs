using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.AdminDeleteUser;

public sealed record AdminDeleteUserCommand(Guid UserId) : ICommand;
