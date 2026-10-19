using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Reports.GetReportByCategory;

public sealed class GetReportByCategoryHandler(IReadReportRepository repository)
{
    public async Task<Result<ByCategoryReportResult>> HandleAsync(ReportPeriodQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ReportPeriodValidator.TryCreate(query, out var period, out var errors))
        {
            return Result<ByCategoryReportResult>.Invalid(errors);
        }

        var rows = await repository.GetByCategoryAsync(period!, cancellationToken);
        return Result<ByCategoryReportResult>.Success(ByCategoryReportResult.From(period!, rows));
    }
}
