using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Users.GetLoggedInUser;

namespace Bookify.Application.Users.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserResponse>;
