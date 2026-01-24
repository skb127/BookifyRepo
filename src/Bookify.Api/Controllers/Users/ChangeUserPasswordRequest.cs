namespace Bookify.Api.Controllers.Users;

public record ChangeUserPasswordRequest(string CurrentPassword, string NewPassword);
