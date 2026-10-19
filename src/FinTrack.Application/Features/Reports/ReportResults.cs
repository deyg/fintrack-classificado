using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Categories;
using FinTrack.Domain.Accounts;

namespace FinTrack.Application.Features.Reports;

public sealed record ReportPeriodResult(DateOnly From, DateOnly To);

public sealed record SummaryReportResult(ReportPeriodResult Period, decimal Income, decimal Expenses, decimal Balance)
{
    // RN12: resultado = receita - despesa, sempre calculado.
    public static SummaryReportResult From(ReportPeriod period, SummaryTotals totals) =>
        new(new ReportPeriodResult(period.From, period.To), totals.Income, totals.Expenses, totals.Income - totals.Expenses);
}

public sealed record CategoryReportItemResult(int CategoryId, string CategoryName, string Nature, decimal Total, int TransactionCount);

public sealed record ByCategoryReportResult(ReportPeriodResult Period, IReadOnlyList<CategoryReportItemResult> Items)
{
    // DEC-REP-008: nome (sem distinção de maiúsculas) e desempate por id.
    public static ByCategoryReportResult From(ReportPeriod period, IEnumerable<CategoryTotalRow> rows) =>
        new(
            new ReportPeriodResult(period.From, period.To),
            rows.OrderBy(r => r.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.CategoryId)
                .Select(r => new CategoryReportItemResult(
                    r.CategoryId, r.CategoryName, CategoryNatureTranslator.ToExternal(r.Nature), r.Total, r.TransactionCount))
                .ToList());
}

public sealed record AccountReportItemResult(
    int AccountId,
    string AccountName,
    int CategoryId,
    string Nature,
    string? Classifier,
    decimal Total,
    int TransactionCount);

public sealed record ByAccountReportResult(ReportPeriodResult Period, IReadOnlyList<AccountReportItemResult> Items)
{
    public static ByAccountReportResult From(ReportPeriod period, IEnumerable<AccountTotalRow> rows) =>
        new(
            new ReportPeriodResult(period.From, period.To),
            rows.OrderBy(r => r.AccountName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.AccountId)
                .Select(r => new AccountReportItemResult(
                    r.AccountId,
                    r.AccountName,
                    r.CategoryId,
                    CategoryNatureTranslator.ToExternal(r.Nature),
                    ClassifierTranslator.ToExternal(r.Classifier),
                    r.Total,
                    r.TransactionCount))
                .ToList());
}

public sealed record ClassifierReportItemResult(string Classifier, decimal Total, int TransactionCount);

public sealed record ByClassifierReportResult(ReportPeriodResult Period, IReadOnlyList<ClassifierReportItemResult> Items)
{
    private static readonly ExpenseClassifier[] Order =
        [ExpenseClassifier.OF, ExpenseClassifier.OV, ExpenseClassifier.NOF, ExpenseClassifier.NOV];

    // DEC-REP-008: sempre os quatro grupos, na ordem OF, OV, NOF, NOV; ausentes entram zerados.
    public static ByClassifierReportResult From(ReportPeriod period, IEnumerable<ClassifierTotalRow> rows)
    {
        var byClassifier = rows
            .GroupBy(r => r.Classifier)
            .ToDictionary(g => g.Key, g => (Total: g.Sum(r => r.Total), Count: g.Sum(r => r.TransactionCount)));

        return new(
            new ReportPeriodResult(period.From, period.To),
            Order.Select(c =>
            {
                var (total, count) = byClassifier.GetValueOrDefault(c);
                return new ClassifierReportItemResult(ClassifierTranslator.ToExternal(c)!, total, count);
            }).ToList());
    }
}
