using FinTrack.Domain.Categories;
using FluentValidation;

namespace FinTrack.Application.Features.Categories.CreateCategory;

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("O nome é obrigatório.")
            .Must(name => name is null || name.Trim().Length <= Category.NameMaxLength)
            .WithMessage($"O nome deve ter no máximo {Category.NameMaxLength} caracteres.");

        RuleFor(x => x.Nature)
            .Must(nature => CategoryNatureTranslator.TryToDomain(nature, out _))
            .WithMessage($"A natureza deve ser {CategoryNatureTranslator.Income} ou {CategoryNatureTranslator.Expense}.");
    }
}
