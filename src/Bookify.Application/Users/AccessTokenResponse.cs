namespace Bookify.Application.Users;

public sealed record AccessTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn, int RefreshExpiresIn);
