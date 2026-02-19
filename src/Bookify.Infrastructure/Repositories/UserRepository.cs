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
}
