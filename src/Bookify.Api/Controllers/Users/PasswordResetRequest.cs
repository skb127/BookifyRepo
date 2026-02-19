namespace Bookify.Api.Controllers.Users;

public record PasswordResetRequest(string Token, string NewPassword);