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
                               u.ban_count AS BanCount
                           FROM users u
                           WHERE u.id = @UserId;

                           SELECT r.name
                           FROM role_user ru
                           INNER JOIN roles r ON ru.roles_id = r.id
                           WHERE ru.users_id = @UserId;
                           """;

        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            sql,
            new { request.UserId });

        AdminUserResponse? userResponse = await multi.ReadFirstOrDefaultAsync<AdminUserResponse>();

        if (userResponse is null)
        {
            return Result.Failure<AdminUserResponse>(UserErrors.NotFound);
        }

        IEnumerable<string> roles = await multi.ReadAsync<string>();

        char statusCodeChar = !string.IsNullOrEmpty(userResponse.StatusCode)
            ? userResponse.StatusCode[0]
            : 'U';

        return userResponse with
        {
            StatusName = UserStatus.FromCode(statusCodeChar).Name,
            Roles = roles.ToList()
        };
    }
}
