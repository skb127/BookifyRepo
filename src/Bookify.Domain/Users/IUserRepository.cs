using System.Linq.Expressions;

namespace Bookify.Domain.Users;

public interface IUserRepository
{
    /// <summary>
    /// Gets a user by ID without loading related entities.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by ID including their assigned roles.
    /// </summary>
    Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByIdIgnoringFiltersAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(User user);

    void AddEmailChangeToken(User user);

    void AddPasswordResetToken(User user);

    void AddAccountDeletionToken(User user);

    Task<User?> FindOneAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default);

    Task<User?> GetOneWithIncludesAsync(
        Expression<Func<User, bool>> predicate,
        CancellationToken cancellationToken,
        params Expression<Func<User, object?>>[] includes);

    Task<User?> FindByAccountDeletionTokenAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<int> CountAdminsAsync(CancellationToken cancellationToken = default);
}
