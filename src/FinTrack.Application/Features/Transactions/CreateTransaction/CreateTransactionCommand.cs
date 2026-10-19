namespace FinTrack.Application.Features.Transactions.CreateTransaction;

public sealed record CreateTransactionCommand(int? AccountId, decimal? Amount, DateOnly? RealizedAt, string? Description);
