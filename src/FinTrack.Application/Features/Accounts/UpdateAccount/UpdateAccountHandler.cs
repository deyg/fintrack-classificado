using FinTrack.Application.Common;
using FinTrack.Application.Features.Categories;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Common;
using FluentValidation;

namespace FinTrack.Application.Features.Accounts.UpdateAccount;

public sealed class UpdateAccountHandler(
    IValidator<UpdateAccountCommand> validator,
    ICategoryRepository categories,
    IAccountRepository accounts,
    IAccountHistoryChecker history)
{
    public const string IdField = "id";

    public async Task<Result<AccountResult>> HandleAsync(UpdateAccountCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Id < 1)
        {
            return Invalid(IdField, "O identificador deve ser um inteiro maior ou igual a 1.");
        }

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AccountResult>.Invalid(validation.Errors
                .GroupBy(e => ToFieldName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var account = await accounts.GetByIdAsync(command.Id, cancellationToken);
        if (account is null)
        {
            return Result<AccountResult>.NotFound();
        }

        var targetCategoryId = command.CategoryId ?? account.CategoryId;
        var category = await categories.GetByIdAsync(targetCategoryId, cancellationToken);
        if (category is null)
        {
            return Invalid("categoryId", "A categoria informada não existe.");
        }

        ExpenseClassifier? targetClassifier = account.Classifier;
        if (command.ClassifierProvided)
        {
            targetClassifier = ClassifierTranslator.TryToDomain(command.Classifier, out var parsed) ? parsed : null;
        }

        try
        {
            Account.EnsureCoherentClassification(category.Nature, targetClassifier);
        }
        catch (DomainException ex) when (ex.Code.StartsWith("account.classifier.", StringComparison.Ordinal))
        {
            return Invalid("classifier", ex.Message);
        }

        var newName = command.Name?.Trim();
        var renames = newName is not null && newName != account.Name;
        var reclassifies = targetCategoryId != account.CategoryId || targetClassifier != account.Classifier;

        if (!renames && !reclassifies)
        {
            return Result<AccountResult>.Success(AccountResult.From(account));
        }

        // DEC-RN-002: com lançamentos, categoria e classificador são imutáveis; só o nome muda.
        if (reclassifies && await history.HasTransactionsAsync(account.Id, cancellationToken))
        {
            return Result<AccountResult>.Conflict(
                "A conta possui lançamentos; não é possível alterar categoria nem classificador.");
        }

        if (renames)
        {
            account.Rename(newName);
        }

        if (reclassifies)
        {
            account.Reclassify(targetCategoryId, category.Nature, targetClassifier);
        }

        await accounts.UpdateAsync(account, cancellationToken);

        return Result<AccountResult>.Success(AccountResult.From(account));
    }

    private static Result<AccountResult> Invalid(string field, string message) =>
        Result<AccountResult>.Invalid(new Dictionary<string, string[]> { [field] = [message] });

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName) ? propertyName : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
