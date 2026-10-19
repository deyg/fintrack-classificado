using FinTrack.Api.Errors;
using FinTrack.Application.Features.Reports;
using FinTrack.Application.Features.Reports.GetFinancialSummary;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Api.Endpoints.Reports;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder api)
    {
        var reports = api.MapGroup("/reports").WithTags("Reports");

        reports.MapGet("/summary", GetFinancialSummaryAsync)
            .WithName("getFinancialSummary")
            .Produces<SummaryReportResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return api;
    }

    private static async Task<IResult> GetFinancialSummaryAsync(
        [FromQuery(Name = "from")] DateOnly? dateFrom,
        [FromQuery(Name = "to")] DateOnly? dateTo,
        GetFinancialSummaryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ReportPeriodQuery(dateFrom, dateTo), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : InvalidPeriod(result.ValidationErrors!);
    }

    internal static IResult InvalidPeriod(IReadOnlyDictionary<string, string[]> errors) =>
        ReportPeriodValidator.IsValueError(errors)
            ? ProblemResults.Validation(StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", errors)
            : ProblemResults.Validation(StatusCodes.Status400BadRequest, "Bad Request", errors);
}
