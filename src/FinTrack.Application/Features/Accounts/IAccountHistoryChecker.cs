namespace FinTrack.Application.Features.Accounts;

// Porta para a regra de histórico (DEC-RN-002): contas com lançamentos não podem ser reclassificadas.
public interface IAccountHistoryChecker
{
    Task<bool> HasTransactionsAsync(int accountId, CancellationToken cancellationToken);
}
