using FinTrack.Application.Features.Reports;
using FinTrack.Domain.Categories;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Infrastructure.Persistence;

internal sealed class ReadReportRepository(FinTrackDbContext context) : IReadReportRepository
{
    private IQueryable<ReportRow> Rows(ReportPeriod period) =>
        from transaction in context.Transactions.AsNoTracking()
        join account in context.Accounts on transaction.AccountId equals account.Id
        join category in context.Categories on account.CategoryId equals category.Id
        where transaction.RealizedAt >= period.From && transaction.RealizedAt <= period.To
        select new ReportRow
        {
            Amount = transaction.Amount,
            AccountId = account.Id,
            AccountName = account.Name,
            Classifier = account.Classifier,
            CategoryId = category.Id,
            CategoryName = category.Name,
            Nature = category.Nature,
        };

    public async Task<SummaryTotals> GetSummaryAsync(ReportPeriod period, CancellationToken cancellationToken)
    {
        var totals = await Rows(period)
            .GroupBy(row => row.Nature)
            .Select(group => new { Nature = group.Key, Total = group.Sum(row => row.Amount) })
            .ToListAsync(cancellationToken);

        return new SummaryTotals(
            totals.Where(t => t.Nature == CategoryNature.Income).Sum(t => t.Total),
            totals.Where(t => t.Nature == CategoryNature.Expense).Sum(t => t.Total));
    }

    public async Task<IReadOnlyList<CategoryTotalRow>> GetByCategoryAsync(ReportPeriod period, CancellationToken cancellationToken) =>
        await Rows(period)
            .GroupBy(row => new { row.CategoryId, row.CategoryName, row.Nature })
            .Select(group => new CategoryTotalRow(
                group.Key.CategoryId,
                group.Key.CategoryName,
                group.Key.Nature,
                group.Sum(row => row.Amount),
                group.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AccountTotalRow>> GetByAccountAsync(ReportPeriod period, CancellationToken cancellationToken) =>
        await Rows(period)
            .GroupBy(row => new { row.AccountId, row.AccountName, row.CategoryId, row.Nature, row.Classifier })
            .Select(group => new AccountTotalRow(
                group.Key.AccountId,
                group.Key.AccountName,
                group.Key.CategoryId,
                group.Key.Nature,
                group.Key.Classifier,
                group.Sum(row => row.Amount),
                group.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClassifierTotalRow>> GetByClassifierAsync(ReportPeriod period, CancellationToken cancellationToken) =>
        await Rows(period)
            .Where(row => row.Nature == CategoryNature.Expense && row.Classifier != null)
            .GroupBy(row => row.Classifier!.Value)
            .Select(group => new ClassifierTotalRow(group.Key, group.Sum(row => row.Amount), group.Count()))
            .ToListAsync(cancellationToken);

    private sealed class ReportRow
    {
        public decimal Amount { get; init; }

        public int AccountId { get; init; }

        public string AccountName { get; init; } = string.Empty;

        public FinTrack.Domain.Accounts.ExpenseClassifier? Classifier { get; init; }

        public int CategoryId { get; init; }

        public string CategoryName { get; init; } = string.Empty;

        public CategoryNature Nature { get; init; }
    }
}