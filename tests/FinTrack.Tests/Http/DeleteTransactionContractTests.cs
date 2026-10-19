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

// Contrato deleteTransaction (DELETE /api/v1/transactions/{id}).
public sealed class DeleteTransactionContractTests(SqlServerDatabaseFixture database)
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

    private async Task<(int AccountId, int TransactionId)> SeedAsync()
    {
        await using var context = database.CreateContext();
        var category = Category.Create("Cat " + Guid.NewGuid().ToString("N")[..8], CategoryNature.Expense);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var account = Account.Create(category.Id, "Conta", CategoryNature.Expense, ExpenseClassifier.OF);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        var transaction = Transaction.Create(account.Id, 10m, new DateOnly(2026, 10, 1), null);
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
        return (account.Id, transaction.Id);
    }

    [Fact]
    public void Contrato_openapi_declara_deleteTransaction_com_204_400_404()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: deleteTransaction", StringComparison.Ordinal);
        var block = yaml[start..yaml.IndexOf("  /reports/summary:", start, StringComparison.Ordinal)];

        foreach (var code in new[] { "204", "400", "404" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Existente_retorna_204_remove_e_preserva_a_conta()
    {
        var (accountId, id) = await SeedAsync();

        var response = await _factory.CreateClient().DeleteAsync($"/api/v1/transactions/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        await using var context = database.CreateContext();
        Assert.False(await context.Transactions.AnyAsync(t => t.Id == id));
        Assert.True(await context.Accounts.AnyAsync(a => a.Id == accountId));
    }

    [Fact]
    public async Task Segunda_exclusao_retorna_404()
    {
        var (_, id) = await SeedAsync();
        await _factory.CreateClient().DeleteAsync($"/api/v1/transactions/{id}");

        await AssertProblem(await _factory.CreateClient().DeleteAsync($"/api/v1/transactions/{id}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Inexistente_retorna_404()
    {
        await AssertProblem(await _factory.CreateClient().DeleteAsync("/api/v1/transactions/999999"), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task Id_invalido_retorna_400(string id)
    {
        await AssertProblem(await _factory.CreateClient().DeleteAsync($"/api/v1/transactions/{id}"), HttpStatusCode.BadRequest);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }
}
