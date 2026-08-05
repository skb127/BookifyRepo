namespace Bookify.Api.Controllers.Users.Requests;

public record PasswordResetRequest(string Token, string NewPassword);