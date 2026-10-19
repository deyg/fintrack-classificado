using FinTrack.Application.Common;
using FinTrack.Domain.Categories;
using FluentValidation;

namespace FinTrack.Application.Features.Categories.CreateCategory;

public sealed class CreateCategoryHandler(
    IValidator<CreateCategoryCommand> validator,
    ICategoryRepository repository)
{
    public async Task<Result<CategoryResult>> HandleAsync(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => ToFieldName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return Result<CategoryResult>.Invalid(errors);
        }

        CategoryNatureTranslator.TryToDomain(command.Nature, out var nature);
        var category = Category.Create(command.Name, nature);

        await repository.AddAsync(category, cancellationToken);

        return Result<CategoryResult>.Success(
            new CategoryResult(category.Id, category.Name, CategoryNatureTranslator.ToExternal(category.Nature)));
    }

    private static string ToFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName) ? propertyName : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
