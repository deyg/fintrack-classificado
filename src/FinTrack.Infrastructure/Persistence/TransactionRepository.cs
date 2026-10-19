using FinTrack.Application.Features.Transactions;
using FinTrack.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Infrastructure.Persistence;

internal sealed class TransactionRepository(FinTrackDbContext context) : ITransactionRepository
{
    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        context.Transactions.AsNoTracking().SingleOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);

    public async Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        context.Transactions.Update(transaction);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        await context.Transactions.Where(transaction => transaction.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<TransactionPageData> ListAsync(
        TransactionFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from transaction in context.Transactions.AsNoTracking()
            join account in context.Accounts on transaction.AccountId equals account.Id
            join category in context.Categories on account.CategoryId equals category.Id
            select new { transaction, account, category };

        if (filter.AccountId is { } accountId)
        {
            query = query.Where(row => row.transaction.AccountId == accountId);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(row => row.account.CategoryId == categoryId);
        }

        if (filter.Nature is { } nature)
        {
            query = query.Where(row => row.category.Nature == nature);
        }

        if (filter.Classifier is { } classifier)
        {
            query = query.Where(row => row.account.Classifier == classifier);
        }

        if (filter.From is { } from)
        {
            query = query.Where(row => row.transaction.RealizedAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(row => row.transaction.RealizedAt <= to);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(row => row.transaction.RealizedAt)
            .ThenByDescending(row => row.transaction.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => row.transaction)
            .ToListAsync(cancellationToken);

        return new TransactionPageData(items, total);
    }
}
