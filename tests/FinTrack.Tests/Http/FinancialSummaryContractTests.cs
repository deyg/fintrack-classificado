using System.Net;
using System.Text.Json;
using FinTrack.Application.Features.Reports;
using FinTrack.Application.Features.Reports.GetFinancialSummary;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Tests.Http;

// Contrato getFinancialSummary (GET /api/v1/reports/summary) com SQL Server real isolado por teste.
public sealed class FinancialSummaryContractTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _database = new();
    private WebApplicationFactory<Program> _factory = null!;

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

    private async Task SeedAsync()
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
            Transaction.Create(rent.Id, 1800m, new DateOnly(2026, 10, 1), "limite inferior"),
            Transaction.Create(market.Id, 250.50m, new DateOnly(2026, 10, 10), null),
            Transaction.Create(market.Id, 80m, new DateOnly(2026, 10, 31), "limite superior"),
            Transaction.Create(wage.Id, 5000m, new DateOnly(2026, 10, 5), null),
            Transaction.Create(rent.Id, 1800m, new DateOnly(2026, 9, 30), "fora: antes"),
            Transaction.Create(wage.Id, 999m, new DateOnly(2026, 11, 1), "fora: depois"));
        await context.SaveChangesAsync();
    }

    private async Task<(HttpStatusCode Status, JsonDocument Json, string? ContentType)> GetAsync(string query)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/v1/reports/summary{query}");
        return (response.StatusCode, JsonDocument.Parse(await response.Content.ReadAsStringAsync()),
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void Contrato_openapi_declara_getFinancialSummary_com_200_400_422()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: getFinancialSummary", StringComparison.Ordinal);
        Assert.True(start > 0);
        var end = yaml.IndexOf("  /reports/by-category:", start, StringComparison.Ordinal);
        var block = yaml[start..end];

        foreach (var code in new[] { "200", "400", "422" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Periodo_inclusivo_soma_receitas_despesas_e_calcula_resultado()
    {
        await SeedAsync();

        var (status, json, _) = await GetAsync("?from=2026-10-01&to=2026-10-31");
        using var doc = json;

        Assert.Equal(HttpStatusCode.OK, status);
        var root = json.RootElement;
        Assert.Equal("2026-10-01", root.GetProperty("period").GetProperty("from").GetString());
        Assert.Equal("2026-10-31", root.GetProperty("period").GetProperty("to").GetString());
        Assert.Equal(5000m, root.GetProperty("income").GetDecimal());
        Assert.Equal(2130.50m, root.GetProperty("expenses").GetDecimal());
        Assert.Equal(2869.50m, root.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Periodo_de_um_dia_considera_somente_aquele_dia()
    {
        await SeedAsync();

        var (status, json, _) = await GetAsync("?from=2026-10-31&to=2026-10-31");
        using var doc = json;

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0m, json.RootElement.GetProperty("income").GetDecimal());
        Assert.Equal(80m, json.RootElement.GetProperty("expenses").GetDecimal());
        Assert.Equal(-80m, json.RootElement.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Periodo_sem_lancamentos_retorna_zeros()
    {
        await SeedAsync();

        var (status, json, _) = await GetAsync("?from=2025-01-01&to=2025-01-31");
        using var doc = json;

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal((0m, 0m, 0m), (
            json.RootElement.GetProperty("income").GetDecimal(),
            json.RootElement.GetProperty("expenses").GetDecimal(),
            json.RootElement.GetProperty("balance").GetDecimal()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-10-01")]
    [InlineData("?to=2026-10-31")]
    [InlineData("?from=2026-13-01&to=2026-10-31")]
    [InlineData("?from=abc&to=2026-10-31")]
    public async Task Periodo_ausente_ou_malformado_retorna_400_problem_json(string query)
    {
        var (status, json, contentType) = await GetAsync(query);
        using var doc = json;

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("application/problem+json", contentType);
    }

    [Fact]
    public async Task Periodo_invertido_retorna_422_com_erro_period()
    {
        var (status, json, contentType) = await GetAsync("?from=2026-11-01&to=2026-10-31");
        using var doc = json;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("application/problem+json", contentType);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("period", out _));
    }

    [Fact]
    public async Task Consulta_nao_persiste_totais()
    {
        await SeedAsync();
        await using var before = _database.CreateContext();
        var count = await before.Transactions.CountAsync();

        var (_, json, _) = await GetAsync("?from=2026-10-01&to=2026-10-31");
        json.Dispose();

        await using var after = _database.CreateContext();
        Assert.Equal(count, await after.Transactions.CountAsync());
    }
}
