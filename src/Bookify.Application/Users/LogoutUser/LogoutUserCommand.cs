using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;

namespace Bookify.Application.Users.LogoutUser;

public sealed record LogoutUserCommand(string RefreshToken) : ICommand;
