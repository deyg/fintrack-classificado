using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Transactions.DeleteTransaction;

public sealed class DeleteTransactionHandler(ITransactionRepository repository)
{
    public async Task<Result<bool>> HandleAsync(DeleteTransactionCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Id < 1)
        {
            return Result<bool>.Invalid(new Dictionary<string, string[]>
            {
                ["id"] = ["O identificador deve ser um inteiro maior ou igual a 1."],
            });
        }

        return await repository.DeleteAsync(command.Id, cancellationToken)
            ? Result<bool>.Success(true)
            : Result<bool>.NotFound();
    }
}
