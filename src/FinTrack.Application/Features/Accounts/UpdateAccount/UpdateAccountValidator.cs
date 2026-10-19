using FinTrack.Domain.Accounts;
using FluentValidation;

namespace FinTrack.Application.Features.Accounts.UpdateAccount;

public sealed class UpdateAccountValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountValidator()
    {
        RuleFor(x => x)
            .Must(x => x.CategoryId is not null || x.Name is not null || x.ClassifierProvided)
            .WithName("body")
            .WithMessage("Informe ao menos um campo: categoryId, name ou classifier.");

        When(x => x.CategoryId is not null, () =>
            RuleFor(x => x.CategoryId)
                .Must(id => id >= 1)
                .WithMessage("A categoria deve ser um inteiro maior ou igual a 1."));

        When(x => x.Name is not null, () =>
            RuleFor(x => x.Name)
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithMessage("O nome não pode ser vazio.")
                .Must(name => name!.Trim().Length <= Account.NameMaxLength)
                .WithMessage($"O nome deve ter no máximo {Account.NameMaxLength} caracteres."));

        When(x => x.Classifier is not null, () =>
            RuleFor(x => x.Classifier)
                .Must(value => ClassifierTranslator.TryToDomain(value, out _))
                .WithMessage("O classificador deve ser OF, OV, NOF ou NOV."));
    }
}
