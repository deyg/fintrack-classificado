namespace FinTrack.Application.Features.Transactions.ListAccountTransactions;

public sealed record ListAccountTransactionsQuery(int AccountId, DateOnly? From, DateOnly? To, int? Page, int? PageSize);
