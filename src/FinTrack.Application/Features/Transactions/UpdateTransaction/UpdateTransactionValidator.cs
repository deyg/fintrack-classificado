using FinTrack.Application.Features.Transactions.CreateTransaction;
using FinTrack.Domain.Transactions;
using FluentValidation;

namespace FinTrack.Application.Features.Transactions.UpdateTransaction;

public sealed class UpdateTransactionValidator : AbstractValidator<UpdateTransactionCommand>
{
    public UpdateTransactionValidator()
    {
        RuleFor(x => x)
            .Must(x => x.AccountId is not null || x.Amount is not null || x.RealizedAt is not null || x.DescriptionProvided)
            .WithName("body")
            .WithMessage("Informe ao menos um campo: accountId, amount, realizedAt ou description.");

        When(x => x.AccountId is not null, () =>
            RuleFor(x => x.AccountId)
                .Must(id => id >= 1)
                .WithMessage("A conta deve ser um inteiro maior ou igual a 1."));

        When(x => x.Amount is not null, () =>
            RuleFor(x => x.Amount)
                .Must(amount => amount > 0)
                .WithMessage("O valor deve ser maior que zero.")
                .Must(amount => decimal.Round(amount!.Value, 2) == amount)
                .WithMessage("O valor admite no máximo duas casas decimais.")
                .Must(amount => amount <= CreateTransactionValidator.MaxAmount)
                .WithMessage("O valor excede o máximo permitido."));

        When(x => x.RealizedAt is not null, () =>
            RuleFor(x => x.RealizedAt)
                .Must(date => date != default(DateOnly))
                .WithMessage("A data de realização é inválida."));

        When(x => x.DescriptionProvided && x.Description is not null, () =>
            RuleFor(x => x.Description)
                .Must(description => description!.Trim().Length <= Transaction.DescriptionMaxLength)
                .WithMessage($"A descrição deve ter no máximo {Transaction.DescriptionMaxLength} caracteres."));
    }
}
