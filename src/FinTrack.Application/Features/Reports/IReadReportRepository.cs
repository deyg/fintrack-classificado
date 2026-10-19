using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;

namespace FinTrack.Application.Features.Reports;

public sealed record SummaryTotals(decimal Income, decimal Expenses);

public sealed record CategoryTotalRow(int CategoryId, string CategoryName, CategoryNature Nature, decimal Total, int TransactionCount);

public sealed record AccountTotalRow(
    int AccountId,
    string AccountName,
    int CategoryId,
    CategoryNature Nature,
    ExpenseClassifier? Classifier,
    decimal Total,
    int TransactionCount);

public sealed record ClassifierTotalRow(ExpenseClassifier Classifier, decimal Total, int TransactionCount);

// Porta somente leitura (RN12): agrega lançamentos no período inclusivo; nenhum saldo é persistido.
// Retorna apenas grupos com lançamentos; ordenação e complemento de grupos ficam nos resultados da Application.
public interface IReadReportRepository
{
    Task<SummaryTotals> GetSummaryAsync(ReportPeriod period, CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryTotalRow>> GetByCategoryAsync(ReportPeriod period, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountTotalRow>> GetByAccountAsync(ReportPeriod period, CancellationToken cancellationToken);

    // Somente despesas (RN03/RN04).
    Task<IReadOnlyList<ClassifierTotalRow>> GetByClassifierAsync(ReportPeriod period, CancellationToken cancellationToken);
}
