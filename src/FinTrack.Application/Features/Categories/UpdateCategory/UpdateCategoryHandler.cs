using FinTrack.Application.Common;
using FluentValidation;

namespace FinTrack.Application.Features.Categories.UpdateCategory;

public sealed class UpdateCategoryHandler(
    IValidator<UpdateCategoryCommand> validator,
    ICategoryRepository repository)
{
    public const string IdField = "id";

    public async Task<Result<CategoryResult>> HandleAsync(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Id < 1)
        {
            return Result<CategoryResult>.Invalid(new Dictionary<string, string[]>
            {
                [IdField] = ["O identificador deve ser um inteiro maior ou igual a 1."],
            });
        }

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CategoryResult>.Invalid(validation.Errors
                .GroupBy(e => ToFieldName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var category = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (category is null)
        {
            return Result<CategoryResult>.NotFound();
        }

        var newName = command.Name?.Trim();
        CategoryNatureTranslator.TryToDomain(command.Nature, out var newNature);
        var renames = newName is not null && newName != category.Name;
        var changesNature = command.Nature is not null && newNature != category.Nature;

        if (!renames && !changesNature)
        {
            return Result<CategoryResult>.Success(ToResult(category));
        }

        // API-P01 e DEC-RN-002: com contas vinculadas, renomear e mudar a natureza são bloqueados.
        if (await repository.HasAccountsAsync(category.Id, cancellationToken))
        {
            return Result<CategoryResult>.Conflict(
                "A categoria possui contas vinculadas; não é possível alterar o nome nem a natureza.");
        }

        if (renames)
        {
            category.Rename(newName);
        }

        if (changesNature)
        {
            category.ChangeNature(newNature);
        }

        await repository.UpdateAsync(category, cancellationToken);

        return Result<CategoryResult>.Success(ToResult(category));
    }

    private static CategoryResult ToResult(Domain.Categories.Category category) =>
        new(category.Id, category.Name, CategoryNatureTranslator.ToExternal(category.Nature));

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName) ? propertyName : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
