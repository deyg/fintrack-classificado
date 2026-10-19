using FinTrack.Domain.Transactions;

namespace FinTrack.Application.Features.Transactions;

public sealed record TransactionResult(int Id, int AccountId, decimal Amount, DateOnly RealizedAt, string? Description)
{
    public static TransactionResult From(Transaction transaction) =>
        new(transaction.Id, transaction.AccountId, transaction.Amount, transaction.RealizedAt, transaction.Description);
}
