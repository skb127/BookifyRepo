using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.RegisterGuest;

public sealed record RegisterGuestCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    DateOnly? DateOfBirth) : ICommand<Guid>;
