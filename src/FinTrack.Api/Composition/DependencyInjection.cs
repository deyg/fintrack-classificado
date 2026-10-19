using FinTrack.Api.Errors;
using FinTrack.Application;
using FinTrack.Infrastructure;

namespace FinTrack.Api.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddFinTrackApi(this IServiceCollection services, IConfiguration configuration)
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
        services.AddApplication();
        services.AddInfrastructure(configuration.GetConnectionString("FinTrack"));

        return services;
    }
}
