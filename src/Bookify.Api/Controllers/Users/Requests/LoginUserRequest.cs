namespace Bookify.Api.Controllers.Users.Requests;

public record LoginUserRequest(
    string Email,
    string Password);
