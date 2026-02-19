using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.UpdateUserProfile;

public sealed record UpdateUserProfileCommand(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    DateOnly DateOfBirth,
    string Password) : ICommand;
