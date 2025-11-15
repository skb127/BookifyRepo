namespace Bookify.Application.Users.GetLoggedInUser;

public sealed record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email);
