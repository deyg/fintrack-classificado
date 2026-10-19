using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Transactions;

public sealed record TransactionPageResult(IReadOnlyList<TransactionResult> Items, PaginationResult Pagination);
