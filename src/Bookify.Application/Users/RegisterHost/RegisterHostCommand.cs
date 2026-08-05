using Bookify.Application.Abstractions.Messaging;

namespace Bookify.Application.Users.RegisterHost;

public sealed record RegisterHostCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    DateOnly? DateOfBirth,
    string PhoneNumber) : ICommand<Guid>;
