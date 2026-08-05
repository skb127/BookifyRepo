using System.Text.Json.Serialization;

namespace Bookify.Api.Controllers.Users.Requests;

public sealed record UpdateUserProfileRequest(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    [property: JsonRequired] DateOnly DateOfBirth,
    string Password);
