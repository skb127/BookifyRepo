using System.Data;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using Dapper;

namespace Bookify.Application.Users.GetUserById;

internal sealed class GetUserByIdQueryHandler
    : IQueryHandler<GetUserByIdQuery, AdminUserResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetUserByIdQueryHandler(ISqlConnectionFactory sqlConnectionFactory) =>
        _sqlConnectionFactory = sqlConnectionFactory;

    public async Task<Result<AdminUserResponse>> Handle(
        GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
                           SELECT
                               u.id AS Id,
                               u.first_name AS FirstName,
                               u.last_name AS LastName,
                               u.email AS Email,
                               u.date_of_birth AS DateOfBirth,
                               u.phone_number AS PhoneNumber,
                               u.identity_id AS IdentityId,
                               u.stripe_customer_id AS StripeCustomerId,
                               u.status AS StatusCode,
                               u.deleted_at AS DeletedAt,
                               u.deletion_scheduled_at AS DeletionScheduledAt,
                               u.last_modified_on AS LastModifiedOn,
                               u.ban_count AS BanCount,
                               r.name AS RoleName
                           FROM users u
                           LEFT JOIN role_user ru ON u.id = ru.users_id
                           LEFT JOIN roles r ON ru.roles_id = r.id
                           WHERE u.id = @UserId
                           """;

        AdminUserResponse? userResponse = null;
        List<string> roles = [];

        await connection.QueryAsync<AdminUserResponse, string, AdminUserResponse>(
            sql,
            (user, roleName) =>
            {
                userResponse ??= user;

                if (!string.IsNullOrEmpty(roleName))
                {
                    roles.Add(roleName);
                }

                return user;
            },
            new
            {
                request.UserId
            },
            splitOn: "RoleName");

        if (userResponse is null)
        {
            return Result.Failure<AdminUserResponse>(UserErrors.NotFound);
        }

        return userResponse with
        {
            StatusName = UserStatus.FromCode(userResponse.StatusCode).Name,
            Roles = roles
        };
    }
}
