using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.LogoutUser;

public sealed record LogoutUserCommand(string RefreshToken) : ICommand;
