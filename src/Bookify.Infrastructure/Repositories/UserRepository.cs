using Bookify.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Infrastructure.Repositories;

internal sealed class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public override async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await DbContext.Set<User>()
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext.Set<User>()
            .Include(user => user.Roles)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<User?> GetByIdIgnoringFiltersAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbContext.Set<User>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public override void Add(User user)
    {
        foreach (Role role in user.Roles)
        {
            DbContext.Attach(role);
        }

        DbContext.Add(user);
    }

    public void AddEmailChangeToken(User user)
    {
        if (user.EmailChangeToken is not null)
        {
            DbContext.Add(user.EmailChangeToken);
        }
    }

    public void AddPasswordResetToken(User user)
    {
        if (user.PasswordResetToken is not null)
        {
            DbContext.Add(user.PasswordResetToken);
        }
    }

    public void AddAccountDeletionToken(User user)
    {
        if (user.AccountDeletionToken is not null)
        {
            DbContext.Add(user.AccountDeletionToken);
        }
    }

    public async Task<User?> FindByAccountDeletionTokenAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await DbContext.Set<User>()
            .Include(u => u.AccountDeletionToken)
            .FirstOrDefaultAsync(u => u.AccountDeletionToken != null && u.AccountDeletionToken.TokenHash == tokenHash, cancellationToken);

    public async Task<int> CountAdminsAsync(CancellationToken cancellationToken = default) =>
        await DbContext.Set<User>()
            .Where(u => u.Roles.Any(r => r.Id == Role.Admin.Id))
            .CountAsync(cancellationToken);
}
