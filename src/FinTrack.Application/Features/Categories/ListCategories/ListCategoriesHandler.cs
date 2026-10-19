using FinTrack.Application.Common;
using FinTrack.Domain.Categories;

namespace FinTrack.Application.Features.Categories.ListCategories;

public sealed class ListCategoriesHandler(ICategoryRepository repository)
{
    public async Task<Result<IReadOnlyList<CategoryResult>>> HandleAsync(
        ListCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CategoryNature? filter = null;
        if (query.Nature is not null)
        {
            if (!CategoryNatureTranslator.TryToDomain(query.Nature, out var nature))
            {
                return Result<IReadOnlyList<CategoryResult>>.Invalid(new Dictionary<string, string[]>
                {
                    ["nature"] = [$"A natureza deve ser {CategoryNatureTranslator.Income} ou {CategoryNatureTranslator.Expense}."],
                });
            }

            filter = nature;
        }

        var categories = await repository.ListAsync(filter, cancellationToken);

        return Result<IReadOnlyList<CategoryResult>>.Success(
            categories
                .Select(c => new CategoryResult(c.Id, c.Name, CategoryNatureTranslator.ToExternal(c.Nature)))
                .ToList());
    }
}
