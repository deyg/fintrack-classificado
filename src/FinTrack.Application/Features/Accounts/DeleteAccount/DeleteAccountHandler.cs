using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Accounts.DeleteAccount;

public sealed class DeleteAccountHandler(IAccountRepository repository, IAccountHistoryChecker history)
{
    public const string IdField = "id";

    public async Task<Result<bool>> HandleAsync(DeleteAccountCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Id < 1)
        {
            return Result<bool>.Invalid(new Dictionary<string, string[]>
            {
                [IdField] = ["O identificador deve ser um inteiro maior ou igual a 1."],
            });
        }

        if (await repository.GetByIdAsync(command.Id, cancellationToken) is null)
        {
            return Result<bool>.NotFound();
        }

        if (await history.HasTransactionsAsync(command.Id, cancellationToken))
        {
            return Result<bool>.Conflict("A conta possui lançamentos vinculados e não pode ser excluída.");
        }

        return await repository.DeleteAsync(command.Id, cancellationToken)
            ? Result<bool>.Success(true)
            : Result<bool>.NotFound();
    }
}
