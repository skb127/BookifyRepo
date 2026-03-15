using System.Data;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Users.GetLoggedInUser;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using Dapper;

namespace Bookify.Application.Users.GetUserById;

internal sealed class GetUserByIdQueryHandler
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetUserByIdQueryHandler(ISqlConnectionFactory sqlConnectionFactory) =>
        _sqlConnectionFactory = sqlConnectionFactory;

    public async Task<Result<UserResponse>> Handle(
        GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               id AS Id,
                               first_name AS FirstName,
                               last_name AS LastName,
                               email AS Email,
                               date_of_birth AS DateOfBirth,
                               phone_number AS PhoneNumber
                           FROM users
                           WHERE id = @UserId
                           """;

        UserResponse? user = await connection.QuerySingleOrDefaultAsync<UserResponse>(
            sql,
            new
            {
                request.UserId
            });

        return user ?? Result.Failure<UserResponse>(UserErrors.NotFound);
    }
}
