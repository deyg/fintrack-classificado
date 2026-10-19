namespace FinTrack.Api.Errors;

// Problem Details padronizados (DEC-ERR-005): type https://httpstatuses.com/<status>, instance = rota.
internal static class ProblemResults
{
    public static IResult NotFound(HttpContext http, string detail) =>
        Create(http, StatusCodes.Status404NotFound, "Not Found", detail);

    public static IResult Conflict(HttpContext http, string detail) =>
        Create(http, StatusCodes.Status409Conflict, "Conflict", detail);

    public static IResult Validation(int status, string title, IReadOnlyDictionary<string, string[]> errors) =>
        Results.ValidationProblem(
            errors.ToDictionary(e => e.Key, e => e.Value),
            title: title,
            type: $"https://httpstatuses.com/{status}",
            statusCode: status);

    private static IResult Create(HttpContext http, int status, string title, string detail) =>
        Results.Problem(
            title: title,
            detail: detail,
            type: $"https://httpstatuses.com/{status}",
            instance: http.Request.Path,
            statusCode: status);
}
