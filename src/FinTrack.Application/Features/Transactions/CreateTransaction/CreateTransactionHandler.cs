using FinTrack.Application.Common;
using FinTrack.Application.Features.Accounts;
using FinTrack.Domain.Common;
using FinTrack.Domain.Transactions;
using FluentValidation;

namespace FinTrack.Application.Features.Transactions.CreateTransaction;

public sealed class CreateTransactionHandler(
    IValidator<CreateTransactionCommand> validator,
    IAccountRepository accounts,
    ITransactionRepository transactions,
    TimeProvider timeProvider)
{
    public async Task<Result<TransactionResult>> HandleAsync(CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<TransactionResult>.Invalid(validation.Errors
                .GroupBy(e => ToFieldName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        // RN07: a data de realização não pode ser posterior à data corrente (fuso local do servidor).
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        if (command.RealizedAt!.Value > today)
        {
            return Invalid("realizedAt", "A data de realização não pode estar no futuro.");
        }

        var account = await accounts.GetByIdAsync(command.AccountId!.Value, cancellationToken);
        if (account is null)
        {
            return Invalid("accountId", "A conta informada não existe.");
        }

        // RN09: conta inativa não aceita novos lançamentos.
        if (!account.Active)
        {
            return Result<TransactionResult>.Conflict("A conta está inativa e não aceita novos lançamentos.");
        }

        Transaction transaction;
        try
        {
            transaction = Transaction.Create(command.AccountId!.Value, command.Amount!.Value, command.RealizedAt.Value, command.Description);
        }
        catch (DomainException ex)
        {
            return Invalid(ex.Code.Split('.') is [_, var field, ..] ? ToFieldName(field) : "transaction", ex.Message);
        }

        await transactions.AddAsync(transaction, cancellationToken);

        return Result<TransactionResult>.Success(TransactionResult.From(transaction));
    }

    private static Result<TransactionResult> Invalid(string field, string message) =>
        Result<TransactionResult>.Invalid(new Dictionary<string, string[]> { [field] = [message] });

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName) ? propertyName : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
