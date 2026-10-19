using FinTrack.Application.Features.Accounts;

namespace FinTrack.Infrastructure.Persistence;

// Provisório até TXN: ainda não existem lançamentos. TXN-001+ deve substituir por consulta ao banco.
internal sealed class NoTransactionsHistoryChecker : IAccountHistoryChecker
{
    public Task<bool> HasTransactionsAsync(int accountId, CancellationToken cancellationToken) => Task.FromResult(false);
}
