using FinTrack.Application.Features.Accounts.ChangeAccountStatus;
using FinTrack.Application.Features.Accounts.DeleteAccount;
using FinTrack.Application.Features.Accounts.CreateAccount;
using FinTrack.Application.Features.Accounts.GetAccount;
using FinTrack.Application.Features.Accounts.ListAccounts;
using FinTrack.Application.Features.Accounts.UpdateAccount;
using FinTrack.Application.Features.Categories.CreateCategory;
using FinTrack.Application.Features.Categories.GetCategory;
using FinTrack.Application.Features.Categories.DeleteCategory;
using FinTrack.Application.Features.Categories.ListCategories;
using FinTrack.Application.Features.Categories.UpdateCategory;
using FinTrack.Application.Features.Transactions.CreateTransaction;
using FinTrack.Application.Features.Transactions.ListTransactions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddScoped<ListAccountsHandler>();
        services.AddScoped<GetAccountHandler>();
        services.AddSingleton<IValidator<UpdateAccountCommand>, UpdateAccountValidator>();
        services.AddScoped<UpdateAccountHandler>();
        services.AddScoped<ChangeAccountStatusHandler>();
        services.AddScoped<DeleteAccountHandler>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IValidator<CreateTransactionCommand>, CreateTransactionValidator>();
        services.AddScoped<CreateTransactionHandler>();
        services.AddScoped<ListTransactionsHandler>();

        return services;
    }
}
