using FinTrack.Api.Errors;

namespace FinTrack.Api.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddFinTrackApi(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                var problem = context.ProblemDetails;
                problem.Type = $"https://httpstatuses.com/{problem.Status}";
                problem.Instance = context.HttpContext.Request.Path;
            });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddOpenApi();
        services.AddHealthChecks();

        return services;
    }
}
