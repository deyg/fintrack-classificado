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

// Contrato listAccountTransactions (GET /api/v1/accounts/{id}/transactions); banco isolado por teste.
public sealed class ListAccountTransactionsContractTests : IAsyncLifetime
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

    // Duas contas: A (t0, t1, t2) e B (b0); A sem lançamentos é criada à parte quando necessário.
    private async Task<(int A, int B, int Empty)> SeedAsync()
    {
        await using var context = _database.CreateContext();
        var category = Category.Create("Moradia", CategoryNature.Expense);
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var a = Account.Create(category.Id, "A", CategoryNature.Expense, ExpenseClassifier.OF);
        var b = Account.Create(category.Id, "B", CategoryNature.Expense, ExpenseClassifier.OV);
        var empty = Account.Create(category.Id, "Vazia", CategoryNature.Expense, ExpenseClassifier.NOF);
        context.Accounts.AddRange(a, b, empty);
        await context.SaveChangesAsync();

        context.Transactions.AddRange(
            Transaction.Create(a.Id, 1m, new DateOnly(2026, 10, 5), "t0"),
            Transaction.Create(a.Id, 2m, new DateOnly(2026, 10, 10), "t1"),
            Transaction.Create(a.Id, 3m, new DateOnly(2026, 10, 5), "t2"),
            Transaction.Create(b.Id, 4m, new DateOnly(2026, 10, 7), "b0"));
        await context.SaveChangesAsync();
        return (a.Id, b.Id, empty.Id);
    }

    private async Task<JsonDocument> GetOkAsync(int accountId, string query = "")
    {
        var response = await _factory.CreateClient().GetAsync($"/api/v1/accounts/{accountId}/transactions{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string[] Descriptions(JsonDocument json) =>
        json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("description").GetString()!).ToArray();

    [Fact]
    public void Contrato_openapi_declara_listAccountTransactions_com_200_400_404_422()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: listAccountTransactions", StringComparison.Ordinal);
        Assert.True(start > 0);
        var block = yaml[start..(start + 2500)];

        foreach (var code in new[] { "200", "400", "404", "422" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Lista_apenas_da_conta_ordenada_por_data_e_id_decrescentes()
    {
        var (a, _, _) = await SeedAsync();

        using var json = await GetOkAsync(a);

        Assert.Equal(["t1", "t2", "t0"], Descriptions(json));
        Assert.Equal(3, json.RootElement.GetProperty("pagination").GetProperty("totalItems").GetInt32());
        Assert.Equal(a, json.RootElement.GetProperty("items")[0].GetProperty("accountId").GetInt32());
    }

    [Fact]
    public async Task Conta_existente_sem_lancamentos_retorna_200_vazio()
    {
        var (_, _, empty) = await SeedAsync();

        using var json = await GetOkAsync(empty);

        Assert.Empty(Descriptions(json));
        Assert.Equal(0, json.RootElement.GetProperty("pagination").GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Periodo_e_paginacao_funcionam()
    {
        var (a, _, _) = await SeedAsync();

        using var period = await GetOkAsync(a, "?from=2026-10-05&to=2026-10-05");
        using var page2 = await GetOkAsync(a, "?pageSize=2&page=2");

        Assert.Equal(["t2", "t0"], Descriptions(period));
        Assert.Equal(["t0"], Descriptions(page2));
        Assert.Equal(2, page2.RootElement.GetProperty("pagination").GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Parametros_de_outras_dimensoes_sao_ignorados()
    {
        var (a, b, _) = await SeedAsync();

        using var json = await GetOkAsync(a, $"?accountId={b}&nature=INCOME");

        Assert.Equal(3, Descriptions(json).Length);
    }

    [Fact]
    public async Task Conta_inexistente_retorna_404_com_instance()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/accounts/999999/transactions");

        await AssertProblem(response, HttpStatusCode.NotFound);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("/api/v1/accounts/999999/transactions", json.RootElement.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task Periodo_invertido_retorna_422()
    {
        var (a, _, _) = await SeedAsync();

        await AssertProblem(
            await _factory.CreateClient().GetAsync($"/api/v1/accounts/{a}/transactions?from=2026-10-10&to=2026-10-09"),
            HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=abc")]
    [InlineData("?from=abc")]
    public async Task Parametros_invalidos_retornam_400(string query)
    {
        var (a, _, _) = await SeedAsync();

        await AssertProblem(
            await _factory.CreateClient().GetAsync($"/api/v1/accounts/{a}/transactions{query}"), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task Identificador_invalido_retorna_400(string id)
    {
        await AssertProblem(
            await _factory.CreateClient().GetAsync($"/api/v1/accounts/{id}/transactions"), HttpStatusCode.BadRequest);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }
}
