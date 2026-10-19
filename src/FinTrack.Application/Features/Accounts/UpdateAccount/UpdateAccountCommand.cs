namespace FinTrack.Application.Features.Accounts.UpdateAccount;

// ClassifierProvided distingue "classifier": null (limpar) de classifier ausente (manter).
public sealed record UpdateAccountCommand(
    int Id,
    int? CategoryId,
    string? Name,
    bool ClassifierProvided,
    string? Classifier);
