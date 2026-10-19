using FinTrack.Domain.Categories;
using FluentValidation;

namespace FinTrack.Application.Features.Categories.UpdateCategory;

public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Name is not null || x.Nature is not null)
            .WithName("body")
            .WithMessage("Informe ao menos um campo: name ou nature.");

        When(x => x.Name is not null, () =>
            RuleFor(x => x.Name)
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithMessage("O nome não pode ser vazio.")
                .Must(name => name!.Trim().Length <= Category.NameMaxLength)
                .WithMessage($"O nome deve ter no máximo {Category.NameMaxLength} caracteres."));

        When(x => x.Nature is not null, () =>
            RuleFor(x => x.Nature)
                .Must(nature => CategoryNatureTranslator.TryToDomain(nature, out _))
                .WithMessage($"A natureza deve ser {CategoryNatureTranslator.Income} ou {CategoryNatureTranslator.Expense}."));
    }
}
