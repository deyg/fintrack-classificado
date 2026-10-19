using FinTrack.Domain.Common;

namespace FinTrack.Domain.Transactions;

public sealed class Transaction
{
    public const int DescriptionMaxLength = 500;

    private Transaction(int accountId, decimal amount, DateOnly realizedAt, string? description)
    {
        AccountId = accountId;
        Amount = amount;
        RealizedAt = realizedAt;
        Description = description;
    }

    public int Id { get; private set; }

    public int AccountId { get; private set; }

    public decimal Amount { get; private set; }

    public DateOnly RealizedAt { get; private set; }

    public string? Description { get; private set; }

    // RN07 (data não futura) depende do relógio e é validada no caso de uso via TimeProvider.
    public static Transaction Create(int accountId, decimal amount, DateOnly realizedAt, string? description) =>
        new(ValidateAccount(accountId), ValidateAmount(amount), ValidateDate(realizedAt), NormalizeDescription(description));

    public void Change(int accountId, decimal amount, DateOnly realizedAt, string? description)
    {
        var validatedAccount = ValidateAccount(accountId);
        var validatedAmount = ValidateAmount(amount);
        var validatedDate = ValidateDate(realizedAt);
        var validatedDescription = NormalizeDescription(description);

        AccountId = validatedAccount;
        Amount = validatedAmount;
        RealizedAt = validatedDate;
        Description = validatedDescription;
    }

    private static int ValidateAccount(int accountId) =>
        accountId < 1
            ? throw new DomainException("transaction.account.required", "O lançamento deve pertencer a uma conta válida.")
            : accountId;

    private static decimal ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new DomainException("transaction.amount.not_positive", "O valor do lançamento deve ser maior que zero.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("transaction.amount.scale", "O valor do lançamento admite no máximo duas casas decimais.");
        }

        return amount;
    }

    private static DateOnly ValidateDate(DateOnly realizedAt) =>
        realizedAt == default
            ? throw new DomainException("transaction.realized_at.required", "A data de realização é obrigatória.")
            : realizedAt;

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > DescriptionMaxLength
            ? throw new DomainException(
                "transaction.description.too_long",
                $"A descrição deve ter no máximo {DescriptionMaxLength} caracteres.")
            : trimmed;
    }
}
