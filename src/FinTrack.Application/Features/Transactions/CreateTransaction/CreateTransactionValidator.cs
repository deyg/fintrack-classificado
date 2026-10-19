using FinTrack.Domain.Transactions;
using FluentValidation;

namespace FinTrack.Application.Features.Transactions.CreateTransaction;

public sealed class CreateTransactionValidator : AbstractValidator<CreateTransactionCommand>
{
    // Limite de decimal(18,2); acima disso o banco rejeitaria a gravação.
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public CreateTransactionValidator()
    {
        RuleFor(x => x.AccountId)
            .Must(id => id is >= 1)
            .WithMessage("A conta é obrigatória e deve ser um inteiro maior ou igual a 1.");

        RuleFor(x => x.Amount)
            .Must(amount => amount is not null)
            .WithMessage("O valor é obrigatório.")
            .Must(amount => amount is null || amount > 0)
            .WithMessage("O valor deve ser maior que zero.")
            .Must(amount => amount is null || decimal.Round(amount.Value, 2) == amount.Value)
            .WithMessage("O valor admite no máximo duas casas decimais.")
            .Must(amount => amount is null || amount <= MaxAmount)
            .WithMessage("O valor excede o máximo permitido.");

        RuleFor(x => x.RealizedAt)
            .Must(date => date is not null && date != default(DateOnly))
            .WithMessage("A data de realização é obrigatória.");

        RuleFor(x => x.Description)
            .Must(description => description is null || description.Trim().Length <= Transaction.DescriptionMaxLength)
            .WithMessage($"A descrição deve ter no máximo {Transaction.DescriptionMaxLength} caracteres.");
    }
}
