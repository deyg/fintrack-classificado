namespace FinTrack.Application.Features.Accounts.ChangeAccountStatus;

public sealed record ChangeAccountStatusCommand(int Id, bool? Active);
