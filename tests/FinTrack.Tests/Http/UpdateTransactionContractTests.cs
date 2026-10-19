using System.Net;
using System.Text;
using System.Text.Json;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using FinTrack.Tests.Transactions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinTrack.Tests.Http;

// Contrato updateTransaction (PATCH /api/v1/transactions/{id}), com relógio fixo em 2026-10-06.
public sealed class UpdateTransactionContractTests(SqlServerDatabaseFixture database)
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
        {
            builder.UseSetting("ConnectionStrings:FinTrack", database.ConnectionString);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero)));
            });
        });
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<int> SeedAccountAsync(bool active = true)
    {
        await using var context = database.CreateContext();
        var category = Category.Create("Cat " + Guid.NewGuid().ToString("N")[..8], CategoryNature.Expense);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var account = Account.Create(category.Id, "Conta", CategoryNature.Expense, ExpenseClassifier.OF);
        if (!active)
        {
            account.Deactivate();
        }

        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private async Task<(int AccountId, int TransactionId)> SeedTransactionAsync()
    {
        var accountId = await SeedAccountAsync();
        await using var context = database.CreateContext();
        var transaction = Transaction.Create(accountId, 100m, new DateOnly(2026, 10, 1), "orig");
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
        return (accountId, transaction.Id);
    }

    private async Task<Transaction> LoadAsync(int id)
    {
        await using var context = database.CreateContext();
        return await context.Transactions.AsNoTracking().SingleAsync(t => t.Id == id);
    }

    private Task<HttpResponseMessage> PatchAsync(string id, string body) =>
        _factory.CreateClient().PatchAsync($"/api/v1/transactions/{id}", new StringContent(body, Encoding.UTF8, "application/json"));

    [Fact]
    public void Contrato_openapi_declara_updateTransaction_com_200_400_404_409_422()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: updateTransaction", StringComparison.Ordinal);
        var block = yaml[start..yaml.IndexOf("    delete:", start, StringComparison.Ordinal)];

        foreach (var code in new[] { "200", "400", "404", "409", "422" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Atualizacao_parcial_retorna_200_e_persiste()
    {
        var (accountId, id) = await SeedTransactionAsync();

        var response = await PatchAsync(id.ToString(), """{"amount":250.75,"realizedAt":"2026-10-06"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(250.75m, json.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("2026-10-06", json.RootElement.GetProperty("realizedAt").GetString());
        Assert.Equal("orig", json.RootElement.GetProperty("description").GetString());
        var stored = await LoadAsync(id);
        Assert.Equal(250.75m, stored.Amount);
        Assert.Equal(accountId, stored.AccountId);
    }

    [Fact]
    public async Task Descricao_nula_limpa_e_texto_substitui()
    {
        var (_, id) = await SeedTransactionAsync();

        var set = await PatchAsync(id.ToString(), """{"description":"  nova  "}""");
        Assert.Equal("nova", (await LoadAsync(id)).Description);

        var cleared = await PatchAsync(id.ToString(), """{"description":null}""");

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null((await LoadAsync(id)).Description);
    }

    [Fact]
    public async Task Troca_para_outra_conta_ativa_persiste()
    {
        var (_, id) = await SeedTransactionAsync();
        var other = await SeedAccountAsync();

        var response = await PatchAsync(id.ToString(), $$"""{"accountId":{{other}}}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(other, (await LoadAsync(id)).AccountId);
    }

    [Fact]
    public async Task Troca_para_conta_inativa_retorna_409_sem_alterar()
    {
        var (accountId, id) = await SeedTransactionAsync();
        var inactive = await SeedAccountAsync(active: false);

        var response = await PatchAsync(id.ToString(), $$"""{"accountId":{{inactive}},"amount":1}""");

        await AssertProblem(response, HttpStatusCode.Conflict);
        var stored = await LoadAsync(id);
        Assert.Equal(accountId, stored.AccountId);
        Assert.Equal(100m, stored.Amount);
    }

    [Fact]
    public async Task Troca_para_conta_inexistente_retorna_422()
    {
        var (_, id) = await SeedTransactionAsync();

        await AssertProblem(await PatchAsync(id.ToString(), """{"accountId":999999}"""), HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Manter_a_conta_mesmo_inativa_permite_corrigir_o_lancamento()
    {
        var (accountId, id) = await SeedTransactionAsync();
        await using (var context = database.CreateContext())
        {
            var account = await context.Accounts.SingleAsync(a => a.Id == accountId);
            account.Deactivate();
            await context.SaveChangesAsync();
        }

        var response = await PatchAsync(id.ToString(), """{"amount":50}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"amount":0}""")]
    [InlineData("""{"amount":-5}""")]
    [InlineData("""{"amount":1.005}""")]
    [InlineData("""{"realizedAt":"2026-10-07"}""")]
    [InlineData("""{"accountId":0}""")]
    public async Task Valores_invalidos_retornam_422(string body)
    {
        var (_, id) = await SeedTransactionAsync();

        await AssertProblem(await PatchAsync(id.ToString(), body), HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("""{"amount":"abc"}""")]
    [InlineData("""{"realizedAt":"nao-e-data"}""")]
    [InlineData("""{"desconhecido":1}""")]
    public async Task Corpo_malformado_ou_com_propriedade_desconhecida_retorna_400(string body)
    {
        var (_, id) = await SeedTransactionAsync();

        await AssertProblem(await PatchAsync(id.ToString(), body), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Inexistente_retorna_404()
    {
        await AssertProblem(await PatchAsync("999999", """{"amount":1}"""), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task Identificador_invalido_retorna_400(string id)
    {
        await AssertProblem(await PatchAsync(id, """{"amount":1}"""), HttpStatusCode.BadRequest);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }
}
