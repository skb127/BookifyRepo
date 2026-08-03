namespace Bookify.Api.Controllers.Users.Requests;

public record ChangeUserPasswordRequest(string CurrentPassword, string NewPassword);
