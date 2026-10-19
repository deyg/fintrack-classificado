using FinTrack.Application.Common;
using FinTrack.Application.Features.Categories;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Common;
using FluentValidation;

namespace FinTrack.Application.Features.Accounts.CreateAccount;

public sealed class CreateAccountHandler(
    IValidator<CreateAccountCommand> validator,
    ICategoryRepository categories,
    IAccountRepository accounts)
{
    public async Task<Result<AccountResult>> HandleAsync(CreateAccountCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AccountResult>.Invalid(validation.Errors
                .GroupBy(e => ToFieldName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var category = await categories.GetByIdAsync(command.CategoryId!.Value, cancellationToken);
        if (category is null)
        {
            return Invalid("categoryId", "A categoria informada não existe.");
        }

        ExpenseClassifier? classifier = ClassifierTranslator.TryToDomain(command.Classifier, out var parsed) ? parsed : null;

        Account account;
        try
        {
            account = Account.Create(category.Id, command.Name, category.Nature, classifier);
        }
        catch (DomainException ex) when (ex.Code.StartsWith("account.classifier.", StringComparison.Ordinal))
        {
            return Invalid("classifier", ex.Message);
        }

        await accounts.AddAsync(account, cancellationToken);

        return Result<AccountResult>.Success(AccountResult.From(account));
    }

    private static Result<AccountResult> Invalid(string field, string message) =>
        Result<AccountResult>.Invalid(new Dictionary<string, string[]> { [field] = [message] });

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName) ? propertyName : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
