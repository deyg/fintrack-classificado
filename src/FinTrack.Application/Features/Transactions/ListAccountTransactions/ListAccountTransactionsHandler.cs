using FinTrack.Application.Common;
using FinTrack.Application.Features.Transactions.ListTransactions;

namespace FinTrack.Application.Features.Transactions.ListAccountTransactions;

// Conta inexistente (404) é verificada antes dos filtros; a listagem reutiliza as regras de listTransactions.
public sealed class ListAccountTransactionsHandler(
    Accounts.IAccountRepository accounts,
    ListTransactionsHandler listing)
{
    public const string IdField = "id";

    public async Task<Result<TransactionPageResult>> HandleAsync(
        ListAccountTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (query.AccountId < 1)
        {
            return Result<TransactionPageResult>.Invalid(new Dictionary<string, string[]>
            {
                [IdField] = ["O identificador deve ser um inteiro maior ou igual a 1."],
            });
        }

        if (await accounts.GetByIdAsync(query.AccountId, cancellationToken) is null)
        {
            return Result<TransactionPageResult>.NotFound();
        }

        return await listing.HandleAsync(
            new ListTransactionsQuery(query.AccountId, null, null, null, query.From, query.To, query.Page, query.PageSize),
            cancellationToken);
    }
}
