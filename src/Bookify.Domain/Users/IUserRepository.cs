using System.Linq.Expressions;

namespace Bookify.Domain.Users;
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(User user);

    Task<User?> FindOneAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken);
}
