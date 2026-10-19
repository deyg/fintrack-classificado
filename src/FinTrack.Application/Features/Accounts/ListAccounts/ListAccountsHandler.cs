using FinTrack.Application.Common;
using FinTrack.Domain.Accounts;

namespace FinTrack.Application.Features.Accounts.ListAccounts;

public sealed class ListAccountsHandler(IAccountRepository repository)
{
    public async Task<Result<IReadOnlyList<AccountResult>>> HandleAsync(
        ListAccountsQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var errors = new Dictionary<string, string[]>();

        if (query.CategoryId is < 1)
        {
            errors["categoryId"] = ["A categoria deve ser um inteiro maior ou igual a 1."];
        }

        ExpenseClassifier? classifier = null;
        if (query.Classifier is not null)
        {
            if (ClassifierTranslator.TryToDomain(query.Classifier, out var parsed))
            {
                classifier = parsed;
            }
            else
            {
                errors["classifier"] = ["O classificador deve ser OF, OV, NOF ou NOV."];
            }
        }

        if (errors.Count > 0)
        {
            return Result<IReadOnlyList<AccountResult>>.Invalid(errors);
        }

        var accounts = await repository.ListAsync(query.CategoryId, query.Active, classifier, cancellationToken);

        return Result<IReadOnlyList<AccountResult>>.Success(accounts.Select(AccountResult.From).ToList());
    }
}
