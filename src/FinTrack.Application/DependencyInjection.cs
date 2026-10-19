using FinTrack.Application.Features.Categories.CreateCategory;
using FinTrack.Application.Features.Categories.GetCategory;
using FinTrack.Application.Features.Categories.ListCategories;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CreateCategoryCommand>, CreateCategoryValidator>();
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<ListCategoriesHandler>();
        services.AddScoped<GetCategoryHandler>();

        return services;
    }
}
