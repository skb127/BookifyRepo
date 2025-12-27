using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.RefreshTokenUser;

public sealed record RefreshTokenUserCommand(string RefreshToken) : ICommand<AccessTokenResponse>;
