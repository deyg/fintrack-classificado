using FinTrack.Domain.Accounts;
using FluentValidation;

namespace FinTrack.Application.Features.Accounts.CreateAccount;

public sealed class CreateAccountValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountValidator()
    {
        RuleFor(x => x.CategoryId)
            .Must(id => id is >= 1)
            .WithMessage("A categoria é obrigatória e deve ser um inteiro maior ou igual a 1.");

        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("O nome é obrigatório.")
            .Must(name => name is null || name.Trim().Length <= Account.NameMaxLength)
            .WithMessage($"O nome deve ter no máximo {Account.NameMaxLength} caracteres.");

        RuleFor(x => x.Classifier)
            .Must(value => value is null || ClassifierTranslator.TryToDomain(value, out _))
            .WithMessage("O classificador deve ser OF, OV, NOF ou NOV.");
    }
}
