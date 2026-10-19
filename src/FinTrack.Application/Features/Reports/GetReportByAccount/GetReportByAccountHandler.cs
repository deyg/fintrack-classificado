using FinTrack.Application.Common;

namespace FinTrack.Application.Features.Reports.GetReportByAccount;

public sealed class GetReportByAccountHandler(IReadReportRepository repository)
{
    public async Task<Result<ByAccountReportResult>> HandleAsync(ReportPeriodQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ReportPeriodValidator.TryCreate(query, out var period, out var errors))
        {
            return Result<ByAccountReportResult>.Invalid(errors);
        }

        var rows = await repository.GetByAccountAsync(period!, cancellationToken);
        return Result<ByAccountReportResult>.Success(ByAccountReportResult.From(period!, rows));
    }
}
