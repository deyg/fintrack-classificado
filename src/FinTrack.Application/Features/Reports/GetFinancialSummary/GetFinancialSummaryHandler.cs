using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Reports.GetFinancialSummary;

public sealed class GetFinancialSummaryHandler(IReadReportRepository repository)
{
    public async Task<Result<SummaryReportResult>> HandleAsync(ReportPeriodQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ReportPeriodValidator.TryCreate(query, out var period, out var errors))
        {
            return Result<SummaryReportResult>.Invalid(errors);
        }

        var totals = await repository.GetSummaryAsync(period!, cancellationToken);
        return Result<SummaryReportResult>.Success(SummaryReportResult.From(period!, totals));
    }
}
