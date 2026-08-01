namespace Bookify.Api.Controllers.Users;

public record RegisterHostRequest(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    DateOnly? DateOfBirth,
    string PhoneNumber);
