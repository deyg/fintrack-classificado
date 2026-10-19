using FinTrack.Application.Common;
using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Categories;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;

namespace FinTrack.Application.Features.Transactions.ListTransactions;

public sealed class ListTransactionsHandler(ITransactionRepository repository)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    // Erro de período invertido: único erro de valor (422); os demais são de parâmetro de consulta (400).
    public const string PeriodField = "to";

    public async Task<Result<TransactionPageResult>> HandleAsync(
        ListTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var errors = new Dictionary<string, string[]>();

        if (query.AccountId is < 1)
        {
            errors["accountId"] = ["A conta deve ser um inteiro maior ou igual a 1."];
        }

        if (query.CategoryId is < 1)
        {
            errors["categoryId"] = ["A categoria deve ser um inteiro maior ou igual a 1."];
        }

        CategoryNature? nature = null;
        if (query.Nature is not null)
        {
            if (CategoryNatureTranslator.TryToDomain(query.Nature, out var parsedNature))
            {
                nature = parsedNature;
            }
            else
            {
                errors["nature"] = ["A natureza deve ser INCOME ou EXPENSE."];
            }
        }

        ExpenseClassifier? classifier = null;
        if (query.Classifier is not null)
        {
            if (ClassifierTranslator.TryToDomain(query.Classifier, out var parsedClassifier))
            {
                classifier = parsedClassifier;
            }
            else
            {
                errors["classifier"] = ["O classificador deve ser OF, OV, NOF ou NOV."];
            }
        }

        var page = query.Page ?? DefaultPage;
        if (page < 1)
        {
            errors["page"] = ["A página deve ser um inteiro maior ou igual a 1."];
        }

        var pageSize = query.PageSize ?? DefaultPageSize;
        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"O tamanho da página deve estar entre 1 e {MaxPageSize}."];
        }

        if (errors.Count == 0 && query.From is { } from && query.To is { } to && to < from)
        {
            errors[PeriodField] = ["A data final deve ser maior ou igual à data inicial."];
        }

        if (errors.Count > 0)
        {
            return Result<TransactionPageResult>.Invalid(errors);
        }

        var filter = new TransactionFilter(query.AccountId, query.CategoryId, nature, classifier, query.From, query.To);
        var data = await repository.ListAsync(filter, page, pageSize, cancellationToken);

        return Result<TransactionPageResult>.Success(new TransactionPageResult(
            data.Items.Select(TransactionResult.From).ToList(),
            PaginationResult.Create(page, pageSize, data.TotalItems)));
    }
}
