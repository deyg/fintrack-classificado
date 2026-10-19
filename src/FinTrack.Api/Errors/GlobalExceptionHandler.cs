using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FinTrack.Api.Errors;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, detail) = exception is BadHttpRequestException bad
            ? (bad.StatusCode, "Requisição malformada.")
            : (StatusCodes.Status500InternalServerError, "Ocorreu um erro inesperado.");

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Erro não tratado em {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = ReasonPhrases.GetReasonPhrase(status),
                Detail = detail,
            },
        });
    }
}
