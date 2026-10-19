using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Categories.GetCategory;

public sealed class GetCategoryHandler(ICategoryRepository repository)
{
    public async Task<Result<CategoryResult>> HandleAsync(GetCategoryQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (query.Id < 1)
        {
            return Result<CategoryResult>.Invalid(new Dictionary<string, string[]>
            {
                ["id"] = ["O identificador deve ser um inteiro maior ou igual a 1."],
            });
        }

        var category = await repository.GetByIdAsync(query.Id, cancellationToken);

        return category is null
            ? Result<CategoryResult>.NotFound()
            : Result<CategoryResult>.Success(
                new CategoryResult(category.Id, category.Name, CategoryNatureTranslator.ToExternal(category.Nature)));
    }
}
