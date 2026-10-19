namespace FinTrack.Application.Features.Accounts.ListAccounts;

public sealed record ListAccountsQuery(int? CategoryId, bool? Active, string? Classifier);
