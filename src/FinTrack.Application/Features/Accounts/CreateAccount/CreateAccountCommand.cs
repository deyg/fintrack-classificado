namespace FinTrack.Application.Features.Accounts.CreateAccount;

public sealed record CreateAccountCommand(int? CategoryId, string? Name, string? Classifier);
