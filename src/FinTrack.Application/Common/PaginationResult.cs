namespace FinTrack.Application.Common;

public sealed record PaginationResult(int Page, int PageSize, int TotalItems, int TotalPages)
{
    public static PaginationResult Create(int page, int pageSize, int totalItems) =>
        new(page, pageSize, totalItems, (int)Math.Ceiling(totalItems / (double)pageSize));
}
