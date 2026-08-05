using System.Linq.Expressions;
using Bookify.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Infrastructure.Repositories;

internal abstract class Repository<T>
    where T : Entity // Generic constraint, requires T to implement the Entity base class
{
    protected readonly ApplicationDbContext DbContext;

    protected Repository(ApplicationDbContext dbContext) => DbContext = dbContext;

    public virtual async Task<T?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
            await DbContext
                .Set<T>()
                .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public virtual void Add(T entity) =>
        DbContext.Add(entity);

    public virtual void Update(T entity) =>
        DbContext.Update(entity);

    public virtual void Delete(T entity) =>
        DbContext.Remove(entity);

    public async Task<T?> FindOneAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
            await DbContext.Set<T>()
                .AsNoTracking()
                .FirstOrDefaultAsync(predicate, cancellationToken);

    public async Task<T?> FindOneIgnoringFiltersAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
            await DbContext.Set<T>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(predicate, cancellationToken);

    public async Task<T?> GetOneWithIncludesAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken,
        params Expression<Func<T, object?>>[] includes)
    {
        IQueryable<T> query = DbContext.Set<T>();

        query = includes.Aggregate(query, (current, include) =>

            current.Include(include));

        return await query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
            await DbContext.Set<T>()
                .AnyAsync(predicate, cancellationToken);
}
