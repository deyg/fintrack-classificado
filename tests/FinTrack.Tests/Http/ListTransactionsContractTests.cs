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

// Contrato listTransactions (GET /api/v1/transactions); banco isolado por teste para resultados determinísticos.
public sealed class ListTransactionsContractTests : IAsyncLifetime
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

    private sealed record Seed(int Housing, int Salary, int Rent, int Market, int Wage, int[] Ids);

    // Rent: despesa OF; Market: despesa NOV (mesma categoria); Wage: receita.
    private async Task<Seed> SeedAsync()
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

        var transactions = new[]
        {
            Transaction.Create(rent.Id, 1800m, new DateOnly(2026, 10, 5), "t0"),
            Transaction.Create(market.Id, 250.50m, new DateOnly(2026, 10, 10), "t1"),
            Transaction.Create(wage.Id, 5000m, new DateOnly(2026, 10, 5), "t2"),
            Transaction.Create(market.Id, 80m, new DateOnly(2026, 10, 10), "t3"),
            Transaction.Create(rent.Id, 1800m, new DateOnly(2026, 9, 5), "t4"),
        };
        context.Transactions.AddRange(transactions);
        await context.SaveChangesAsync();

        return new Seed(housing.Id, salary.Id, rent.Id, market.Id, wage.Id, transactions.Select(t => t.Id).ToArray());
    }

    private async Task<JsonDocument> GetOkAsync(string query)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/v1/transactions{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string[] Descriptions(JsonDocument json) =>
        json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("description").GetString()!).ToArray();

    [Fact]
    public void Contrato_openapi_declara_listTransactions_com_200_400_422()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: listTransactions", StringComparison.Ordinal);
        Assert.True(start > 0);
        var block = yaml[start..yaml.IndexOf("    post:", start, StringComparison.Ordinal)];

        foreach (var code in new[] { "200", "400", "422" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Vazio_retorna_200_com_paginacao_zerada_e_defaults()
    {
        using var json = await GetOkAsync(string.Empty);

        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
        var pagination = json.RootElement.GetProperty("pagination");
        Assert.Equal(1, pagination.GetProperty("page").GetInt32());
        Assert.Equal(20, pagination.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, pagination.GetProperty("totalItems").GetInt32());
        Assert.Equal(0, pagination.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Ordena_por_data_e_id_decrescentes_e_serializa_o_schema()
    {
        await SeedAsync();

        using var json = await GetOkAsync(string.Empty);

        Assert.Equal(["t3", "t1", "t2", "t0", "t4"], Descriptions(json));
        var first = json.RootElement.GetProperty("items")[0];
        Assert.Equal(80m, first.GetProperty("amount").GetDecimal());
        Assert.Equal("2026-10-10", first.GetProperty("realizedAt").GetString());
        Assert.Equal(5, json.RootElement.GetProperty("pagination").GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task Paginacao_respeita_fronteiras_e_pagina_alem_do_fim_vem_vazia()
    {
        await SeedAsync();

        using var page1 = await GetOkAsync("?pageSize=2");
        using var page3 = await GetOkAsync("?pageSize=2&page=3");
        using var page4 = await GetOkAsync("?pageSize=2&page=4");

        Assert.Equal(["t3", "t1"], Descriptions(page1));
        Assert.Equal(3, page1.RootElement.GetProperty("pagination").GetProperty("totalPages").GetInt32());
        Assert.Equal(["t4"], Descriptions(page3));
        Assert.Empty(Descriptions(page4));
        Assert.Equal(4, page4.RootElement.GetProperty("pagination").GetProperty("page").GetInt32());
        Assert.Equal(5, page4.RootElement.GetProperty("pagination").GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task Filtros_por_conta_categoria_natureza_e_classificador()
    {
        var seed = await SeedAsync();

        using var byAccount = await GetOkAsync($"?accountId={seed.Market}");
        using var byCategory = await GetOkAsync($"?categoryId={seed.Housing}");
        using var income = await GetOkAsync("?nature=INCOME");
        using var expense = await GetOkAsync("?nature=EXPENSE");
        using var nov = await GetOkAsync("?classifier=NOV");
        using var unknownAccount = await GetOkAsync("?accountId=999999");

        Assert.Equal(["t3", "t1"], Descriptions(byAccount));
        Assert.Equal(["t3", "t1", "t0", "t4"], Descriptions(byCategory));
        Assert.Equal(["t2"], Descriptions(income));
        Assert.Equal(["t3", "t1", "t0", "t4"], Descriptions(expense));
        Assert.Equal(["t3", "t1"], Descriptions(nov));
        Assert.Empty(Descriptions(unknownAccount));
    }

    [Fact]
    public async Task Periodo_e_inclusivo_nas_duas_pontas_e_filtros_combinam_por_and()
    {
        await SeedAsync();

        using var period = await GetOkAsync("?from=2026-10-05&to=2026-10-10");
        using var single = await GetOkAsync("?from=2026-10-05&to=2026-10-05");
        using var onlyFrom = await GetOkAsync("?from=2026-10-10");
        using var onlyTo = await GetOkAsync("?to=2026-09-30");
        using var combined = await GetOkAsync("?nature=EXPENSE&from=2026-10-05&to=2026-10-05");

        Assert.Equal(["t3", "t1", "t2", "t0"], Descriptions(period));
        Assert.Equal(["t2", "t0"], Descriptions(single));
        Assert.Equal(["t3", "t1"], Descriptions(onlyFrom));
        Assert.Equal(["t4"], Descriptions(onlyTo));
        Assert.Equal(["t0"], Descriptions(combined));
    }

    [Fact]
    public async Task Periodo_invertido_retorna_422()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/transactions?from=2026-10-10&to=2026-10-09");

        await AssertProblem(response, HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=abc")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=abc")]
    [InlineData("?from=abc")]
    [InlineData("?to=2026-13-45")]
    [InlineData("?accountId=0")]
    [InlineData("?accountId=abc")]
    [InlineData("?categoryId=0")]
    [InlineData("?nature=")]
    [InlineData("?nature=X")]
    [InlineData("?classifier=")]
    [InlineData("?classifier=X")]
    public async Task Parametros_de_consulta_invalidos_retornam_400(string query)
    {
        await AssertProblem(await _factory.CreateClient().GetAsync($"/api/v1/transactions{query}"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pagina_maxima_de_100_e_aceita()
    {
        await SeedAsync();

        using var json = await GetOkAsync("?pageSize=100");

        Assert.Equal(100, json.RootElement.GetProperty("pagination").GetProperty("pageSize").GetInt32());
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }
}
