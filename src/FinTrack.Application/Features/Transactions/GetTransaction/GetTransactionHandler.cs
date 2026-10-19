using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Transactions.GetTransaction;

public sealed class GetTransactionHandler(ITransactionRepository repository)
{
    public async Task<Result<TransactionResult>> HandleAsync(GetTransactionQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (query.Id < 1)
        {
            return Result<TransactionResult>.Invalid(new Dictionary<string, string[]>
            {
                ["id"] = ["O identificador deve ser um inteiro maior ou igual a 1."],
            });
        }

        var transaction = await repository.GetByIdAsync(query.Id, cancellationToken);

        return transaction is null
            ? Result<TransactionResult>.NotFound()
            : Result<TransactionResult>.Success(TransactionResult.From(transaction));
    }
}
