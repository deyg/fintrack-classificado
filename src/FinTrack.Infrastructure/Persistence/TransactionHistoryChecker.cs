using FinTrack.Application.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Infrastructure.Persistence;

internal sealed class TransactionHistoryChecker(FinTrackDbContext context) : IAccountHistoryChecker
{
    public Task<bool> HasTransactionsAsync(int accountId, CancellationToken cancellationToken) =>
        context.Transactions.AnyAsync(transaction => transaction.AccountId == accountId, cancellationToken);
}
