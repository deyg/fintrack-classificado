namespace FinTrack.Application.Features.Transactions.ListTransactions;

public sealed record ListTransactionsQuery(
    int? AccountId,
    int? CategoryId,
    string? Nature,
    string? Classifier,
    DateOnly? From,
    DateOnly? To,
    int? Page,
    int? PageSize);
