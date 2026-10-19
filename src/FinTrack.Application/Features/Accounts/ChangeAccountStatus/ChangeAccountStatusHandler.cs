using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Accounts.ChangeAccountStatus;

public sealed class ChangeAccountStatusHandler(IAccountRepository repository)
{
    public const string IdField = "id";

    public async Task<Result<AccountResult>> HandleAsync(ChangeAccountStatusCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Id < 1)
        {
            return Invalid(IdField, "O identificador deve ser um inteiro maior ou igual a 1.");
        }

        if (command.Active is not { } active)
        {
            return Invalid("active", "O campo active é obrigatório.");
        }

        var account = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (account is null)
        {
            return Result<AccountResult>.NotFound();
        }

        // Idempotente: repetir o estado atual retorna 200 sem gravar.
        if (account.Active != active)
        {
            if (active)
            {
                account.Activate();
            }
            else
            {
                account.Deactivate();
            }

            await repository.UpdateAsync(account, cancellationToken);
        }

        return Result<AccountResult>.Success(AccountResult.From(account));
    }

    private static Result<AccountResult> Invalid(string field, string message) =>
        Result<AccountResult>.Invalid(new Dictionary<string, string[]> { [field] = [message] });
}
