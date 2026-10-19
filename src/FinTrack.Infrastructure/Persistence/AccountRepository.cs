using FinTrack.Application.Features.Accounts;
using FinTrack.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Infrastructure.Persistence;

internal sealed class AccountRepository(FinTrackDbContext context) : IAccountRepository
{
    public async Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        context.Accounts.Add(account);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        context.Accounts.AsNoTracking().SingleOrDefaultAsync(account => account.Id == id, cancellationToken);
}
