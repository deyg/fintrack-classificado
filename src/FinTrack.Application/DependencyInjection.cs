using FinTrack.Application.Features.Accounts.CreateAccount;
using FinTrack.Application.Features.Categories.CreateCategory;
using FinTrack.Application.Features.Categories.GetCategory;
using FinTrack.Application.Features.Categories.DeleteCategory;
using FinTrack.Application.Features.Categories.ListCategories;
using FinTrack.Application.Features.Categories.UpdateCategory;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CreateCategoryCommand>, CreateCategoryValidator>();
        services.AddSingleton<IValidator<UpdateCategoryCommand>, UpdateCategoryValidator>();
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<ListCategoriesHandler>();
        services.AddScoped<GetCategoryHandler>();
        services.AddScoped<UpdateCategoryHandler>();
        services.AddScoped<DeleteCategoryHandler>();
        services.AddSingleton<IValidator<CreateAccountCommand>, CreateAccountValidator>();
        services.AddScoped<CreateAccountHandler>();

        return services;
    }
}
