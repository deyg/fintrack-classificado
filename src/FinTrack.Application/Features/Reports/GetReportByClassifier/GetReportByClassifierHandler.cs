using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Reports.GetReportByClassifier;

public sealed class GetReportByClassifierHandler(IReadReportRepository repository)
{
    public async Task<Result<ByClassifierReportResult>> HandleAsync(ReportPeriodQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ReportPeriodValidator.TryCreate(query, out var period, out var errors))
        {
            return Result<ByClassifierReportResult>.Invalid(errors);
        }

        var rows = await repository.GetByClassifierAsync(period!, cancellationToken);
        return Result<ByClassifierReportResult>.Success(ByClassifierReportResult.From(period!, rows));
    }
}
