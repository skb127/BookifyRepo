using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.BanUser;

public sealed record BanUserCommand(Guid UserId) : ICommand;
