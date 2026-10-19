using FinTrack.Application.Common;
using FinTrack.Application.Features.Accounts;
using FinTrack.Domain.Common;
using FluentValidation;

namespace FinTrack.Application.Features.Transactions.UpdateTransaction;

public sealed class UpdateTransactionHandler(
    IValidator<UpdateTransactionCommand> validator,
    IAccountRepository accounts,
    ITransactionRepository transactions,
    TimeProvider timeProvider)
{
    public const string IdField = "id";

    public async Task<Result<TransactionResult>> HandleAsync(UpdateTransactionCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Id < 1)
        {
            return Invalid(IdField, "O identificador deve ser um inteiro maior ou igual a 1.");
        }

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<TransactionResult>.Invalid(validation.Errors
                .GroupBy(e => ToFieldName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var transaction = await transactions.GetByIdAsync(command.Id, cancellationToken);
        if (transaction is null)
        {
            return Result<TransactionResult>.NotFound();
        }

        // RN07: a data de realização não pode ser posterior à data corrente (fuso local do servidor).
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        if (command.RealizedAt is { } realizedAt && realizedAt > today)
        {
            return Invalid("realizedAt", "A data de realização não pode estar no futuro.");
        }

        // RN09: só a conta de destino de uma troca precisa existir e estar ativa.
        var targetAccountId = command.AccountId ?? transaction.AccountId;
        if (targetAccountId != transaction.AccountId)
        {
            var account = await accounts.GetByIdAsync(targetAccountId, cancellationToken);
            if (account is null)
            {
                return Invalid("accountId", "A conta informada não existe.");
            }

            if (!account.Active)
            {
                return Result<TransactionResult>.Conflict("A conta está inativa e não aceita novos lançamentos.");
            }
        }

        try
        {
            transaction.Change(
                targetAccountId,
                command.Amount ?? transaction.Amount,
                command.RealizedAt ?? transaction.RealizedAt,
                command.DescriptionProvided ? command.Description : transaction.Description);
        }
        catch (DomainException ex)
        {
            return Invalid(ex.Code.Split('.') is [_, var field, ..] ? ToFieldName(field) : "transaction", ex.Message);
        }

        await transactions.UpdateAsync(transaction, cancellationToken);

        return Result<TransactionResult>.Success(TransactionResult.From(transaction));
    }

    private static Result<TransactionResult> Invalid(string field, string message) =>
        Result<TransactionResult>.Invalid(new Dictionary<string, string[]> { [field] = [message] });

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName) ? propertyName : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
