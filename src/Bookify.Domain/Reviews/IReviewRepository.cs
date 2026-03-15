using System.Linq.Expressions;

namespace Bookify.Domain.Reviews;

public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Review review);

    Task<bool> ExistsAsync(
        Expression<Func<Review, bool>> predicate,
        CancellationToken cancellationToken = default);
}
