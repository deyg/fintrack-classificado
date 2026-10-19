namespace FinTrack.Application.Features.Transactions.UpdateTransaction;

// DescriptionProvided distingue "description": null (limpar) de descrição ausente (manter).
public sealed record UpdateTransactionCommand(
    int Id,
    int? AccountId,
    decimal? Amount,
    DateOnly? RealizedAt,
    bool DescriptionProvided,
    string? Description);
