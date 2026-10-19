using System.Net;
using System.Text.Json;
using FinTrack.Application.Features.Accounts;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinTrack.Tests.Http;

// Contrato deleteAccount (DELETE /api/v1/accounts/{id}).
public sealed class DeleteAccountContractTests(SqlServerDatabaseFixture database)
    : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private sealed class AlwaysHasTransactions : IAccountHistoryChecker
    {
        public Task<bool> HasTransactionsAsync(int accountId, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private WebApplicationFactory<Program> _factory = null!;
    private WebApplicationFactory<Program> _withHistory = null!;

    public async Task InitializeAsync()
    {
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:FinTrack", database.ConnectionString));
        _withHistory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAccountHistoryChecker>();
                services.AddScoped<IAccountHistoryChecker, AlwaysHasTransactions>();
            }));
    }

    public async Task DisposeAsync()
    {
        await _withHistory.DisposeAsync();
        await _factory.DisposeAsync();
    }

    private async Task<int> SeedAccountAsync()
    {
        await using var context = database.CreateContext();
        var category = Category.Create("Cat " + Guid.NewGuid().ToString("N")[..8], CategoryNature.Expense);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var account = Account.Create(category.Id, "Conta", CategoryNature.Expense, ExpenseClassifier.OF);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private async Task<bool> ExistsAsync(int id)
    {
        await using var context = database.CreateContext();
        return await context.Accounts.AnyAsync(a => a.Id == id);
    }

    [Fact]
    public void Contrato_openapi_declara_deleteAccount_com_204_400_404_409()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: deleteAccount", StringComparison.Ordinal);
        Assert.True(start > 0);
        var block = yaml[start..yaml.IndexOf("  /accounts/{id}/status:", start, StringComparison.Ordinal)];

        foreach (var code in new[] { "204", "400", "404", "409" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Sem_lancamentos_retorna_204_e_remove()
    {
        var id = await SeedAccountAsync();

        var response = await _factory.CreateClient().DeleteAsync($"/api/v1/accounts/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await ExistsAsync(id));
    }

    [Fact]
    public async Task Inexistente_retorna_404()
    {
        await AssertProblem(await _factory.CreateClient().DeleteAsync("/api/v1/accounts/999999"), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("abc")]
    public async Task Id_invalido_retorna_400(string id)
    {
        await AssertProblem(await _factory.CreateClient().DeleteAsync($"/api/v1/accounts/{id}"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Com_lancamentos_retorna_409_e_preserva()
    {
        var id = await SeedAccountAsync();

        var response = await _withHistory.CreateClient().DeleteAsync($"/api/v1/accounts/{id}");

        await AssertProblem(response, HttpStatusCode.Conflict);
        Assert.True(await ExistsAsync(id));
    }

    [Fact]
    public async Task Com_lancamento_real_retorna_409_sem_simulacao()
    {
        var id = await SeedAccountAsync();
        await using (var context = database.CreateContext())
        {
            context.Transactions.Add(FinTrack.Domain.Transactions.Transaction.Create(id, 10m, new DateOnly(2026, 10, 6), null));
            await context.SaveChangesAsync();
        }

        await AssertProblem(await _factory.CreateClient().DeleteAsync($"/api/v1/accounts/{id}"), HttpStatusCode.Conflict);
        Assert.True(await ExistsAsync(id));
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }
}
