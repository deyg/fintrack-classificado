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

    public async Task UpdateAsync(Account account, CancellationToken cancellationToken)
    {
        context.Accounts.Update(account);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Account>> ListAsync(
        int? categoryId,
        bool? active,
        ExpenseClassifier? classifier,
        CancellationToken cancellationToken)
    {
        var query = context.Accounts.AsNoTracking();

        if (categoryId is { } category)
        {
            query = query.Where(account => account.CategoryId == category);
        }

        if (active is { } isActive)
        {
            query = query.Where(account => account.Active == isActive);
        }

        if (classifier is { } value)
        {
            query = query.Where(account => account.Classifier == value);
        }

        return await query.OrderBy(account => account.Id).ToListAsync(cancellationToken);
    }
}
