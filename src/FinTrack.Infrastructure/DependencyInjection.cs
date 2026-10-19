using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Categories;
using FinTrack.Application.Features.Reports;
using FinTrack.Application.Features.Transactions;
using FinTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<FinTrackDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IAccountHistoryChecker, TransactionHistoryChecker>();
        services.AddScoped<IReadReportRepository, ReadReportRepository>();

        return services;
    }
}
