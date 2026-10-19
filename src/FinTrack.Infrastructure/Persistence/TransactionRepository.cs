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
}
