using System.Net;
using System.Text.Json;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Tests.Http;

// Base dos contratos de relatório: SQL Server real isolado por teste e massa comum.
public abstract class ReportContractTestBase : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _database = new();
    private WebApplicationFactory<Program> _factory = null!;

    protected abstract string Path { get; }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await using (var context = _database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:FinTrack", _database.ConnectionString));
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected sealed record Ids(int Housing, int Salary, int Rent, int Market, int Wage);

    // Moradia: Aluguel (OF), Mercado (NOV); Salario: receita. Outubro/2026 + lançamentos fora do período.
    protected async Task<Ids> SeedAsync()
    {
        await using var context = _database.CreateContext();
        var housing = Category.Create("Moradia", CategoryNature.Expense);
        var salary = Category.Create("Salario", CategoryNature.Income);
        context.Categories.AddRange(housing, salary);
        await context.SaveChangesAsync();

        var rent = Account.Create(housing.Id, "Aluguel", CategoryNature.Expense, ExpenseClassifier.OF);
        var market = Account.Create(housing.Id, "Mercado", CategoryNature.Expense, ExpenseClassifier.NOV);
        var wage = Account.Create(salary.Id, "Salario", CategoryNature.Income, null);
        context.Accounts.AddRange(rent, market, wage);
        await context.SaveChangesAsync();

        context.Transactions.AddRange(
            Transaction.Create(rent.Id, 1800m, new DateOnly(2026, 10, 1), null),
            Transaction.Create(market.Id, 250.50m, new DateOnly(2026, 10, 10), null),
            Transaction.Create(market.Id, 80m, new DateOnly(2026, 10, 31), null),
            Transaction.Create(wage.Id, 5000m, new DateOnly(2026, 10, 5), null),
            Transaction.Create(rent.Id, 1800m, new DateOnly(2026, 9, 30), null),
            Transaction.Create(wage.Id, 999m, new DateOnly(2026, 11, 1), null));
        await context.SaveChangesAsync();

        return new Ids(housing.Id, salary.Id, rent.Id, market.Id, wage.Id);
    }

    protected async Task<(HttpStatusCode Status, JsonDocument Json, string? ContentType)> GetAsync(string query)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/v1/reports/{Path}{query}");
        return (response.StatusCode, JsonDocument.Parse(await response.Content.ReadAsStringAsync()),
            response.Content.Headers.ContentType?.MediaType);
    }

    protected async Task<int> CountTransactionsAsync()
    {
        await using var context = _database.CreateContext();
        return await context.Transactions.CountAsync();
    }

    protected static void AssertOpenApiDeclares(string operationId, string nextPath)
    {
        var yaml = File.ReadAllText(System.IO.Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf($"operationId: {operationId}", StringComparison.Ordinal);
        Assert.True(start > 0);
        var end = nextPath.Length == 0 ? yaml.Length : yaml.IndexOf(nextPath, start, StringComparison.Ordinal);
        var block = yaml[start..end];

        foreach (var code in new[] { "200", "400", "422" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    protected async Task AssertInvalidPeriodAsync(string query, HttpStatusCode expected, string? errorKey = null)
    {
        var (status, json, contentType) = await GetAsync(query);
        using var doc = json;

        Assert.Equal(expected, status);
        Assert.Equal("application/problem+json", contentType);
        if (errorKey is not null)
        {
            Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(errorKey, out _));
        }
    }
}
