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

// Contrato getTransaction (GET /api/v1/transactions/{id}).
public sealed class GetTransactionContractTests(SqlServerDatabaseFixture database)
    : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:FinTrack", database.ConnectionString));
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<(int AccountId, int TransactionId)> SeedAsync(string? description)
    {
        await using var context = database.CreateContext();
        var category = Category.Create("Cat " + Guid.NewGuid().ToString("N")[..8], CategoryNature.Expense);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var account = Account.Create(category.Id, "Conta", CategoryNature.Expense, ExpenseClassifier.OF);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        var transaction = Transaction.Create(account.Id, 123.45m, new DateOnly(2026, 3, 4), description);
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
        return (account.Id, transaction.Id);
    }

    [Fact]
    public void Contrato_openapi_declara_getTransaction_com_200_400_404()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: getTransaction", StringComparison.Ordinal);
        var block = yaml[start..yaml.IndexOf("    patch:", start, StringComparison.Ordinal)];

        Assert.Contains("'200':", block);
        Assert.Contains("'400':", block);
        Assert.Contains("'404':", block);
    }

    [Fact]
    public async Task Existente_retorna_200_com_todos_os_campos()
    {
        var (accountId, transactionId) = await SeedAsync("Mercado");

        var response = await _factory.CreateClient().GetAsync($"/api/v1/transactions/{transactionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(transactionId, root.GetProperty("id").GetInt32());
        Assert.Equal(accountId, root.GetProperty("accountId").GetInt32());
        Assert.Equal(123.45m, root.GetProperty("amount").GetDecimal());
        Assert.Equal("2026-03-04", root.GetProperty("realizedAt").GetString());
        Assert.Equal("Mercado", root.GetProperty("description").GetString());
        Assert.Equal(
            ["accountId", "amount", "description", "id", "realizedAt"],
            root.EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Descricao_ausente_retorna_nula()
    {
        var (_, transactionId) = await SeedAsync(null);

        var response = await _factory.CreateClient().GetAsync($"/api/v1/transactions/{transactionId}");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task Inexistente_retorna_404_com_instance()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/transactions/999999");

        await AssertProblem(response, HttpStatusCode.NotFound);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("/api/v1/transactions/999999", json.RootElement.GetProperty("instance").GetString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("2147483648")]
    public async Task Identificador_invalido_retorna_400(string id)
    {
        await AssertProblem(await _factory.CreateClient().GetAsync($"/api/v1/transactions/{id}"), HttpStatusCode.BadRequest);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }
}
