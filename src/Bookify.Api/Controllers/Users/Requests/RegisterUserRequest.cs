namespace Bookify.Api.Controllers.Users.Requests;

public record RegisterUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    DateOnly? DateOfBirth);
