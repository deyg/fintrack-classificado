using System.Net;
using System.Text;
using System.Text.Json;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using FinTrack.Tests.Transactions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinTrack.Tests.Http;

// Contrato createTransaction (POST /api/v1/transactions), com relógio fixo em 2026-10-06.
public sealed class CreateTransactionContractTests(SqlServerDatabaseFixture database)
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

    private Task<HttpResponseMessage> PostAsync(string body) =>
        _factory.CreateClient().PostAsync("/api/v1/transactions", new StringContent(body, Encoding.UTF8, "application/json"));

    [Fact]
    public void Contrato_openapi_declara_createTransaction_com_201_400_409_422()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: createTransaction", StringComparison.Ordinal);
        Assert.True(start > 0);
        var block = yaml[start..yaml.IndexOf("  /transactions/{id}:", start, StringComparison.Ordinal)];

        foreach (var code in new[] { "201", "400", "409", "422" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Valido_retorna_201_location_e_corpo()
    {
        var accountId = await SeedAccountAsync();

        var response = await PostAsync($$"""{"accountId":{{accountId}},"amount":1800.5,"realizedAt":"2026-10-06","description":" Aluguel "}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetInt32();
        Assert.Equal($"/api/v1/transactions/{id}", response.Headers.Location!.ToString());
        Assert.Equal(accountId, json.RootElement.GetProperty("accountId").GetInt32());
        Assert.Equal(1800.5m, json.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("2026-10-06", json.RootElement.GetProperty("realizedAt").GetString());
        Assert.Equal("Aluguel", json.RootElement.GetProperty("description").GetString());

        await using var context = database.CreateContext();
        var stored = await context.Transactions.AsNoTracking().SingleAsync(t => t.Id == id);
        Assert.Equal(1800.50m, stored.Amount);
    }

    [Fact]
    public async Task Descricao_ausente_ou_nula_e_aceita()
    {
        var accountId = await SeedAccountAsync();

        var response = await PostAsync($$"""{"accountId":{{accountId}},"amount":0.01,"realizedAt":"2026-10-05"}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task Conta_inativa_retorna_409_sem_gravar()
    {
        var accountId = await SeedAccountAsync(active: false);

        await AssertProblem(
            await PostAsync($$"""{"accountId":{{accountId}},"amount":10,"realizedAt":"2026-10-06"}"""),
            HttpStatusCode.Conflict);

        await using var context = database.CreateContext();
        Assert.False(await context.Transactions.AnyAsync(t => t.AccountId == accountId));
    }

    [Fact]
    public async Task Data_futura_retorna_422_e_data_de_hoje_e_aceita()
    {
        var accountId = await SeedAccountAsync();

        await AssertProblem(
            await PostAsync($$"""{"accountId":{{accountId}},"amount":10,"realizedAt":"2026-10-07"}"""),
            HttpStatusCode.UnprocessableEntity);
        Assert.Equal(
            HttpStatusCode.Created,
            (await PostAsync($$"""{"accountId":{{accountId}},"amount":10,"realizedAt":"2026-10-06"}""")).StatusCode);
    }

    [Theory]
    [InlineData("""{"accountId":999999,"amount":10,"realizedAt":"2026-10-06"}""")]
    [InlineData("""{"amount":10,"realizedAt":"2026-10-06"}""")]
    [InlineData("""{"accountId":0,"amount":10,"realizedAt":"2026-10-06"}""")]
    [InlineData("""{}""")]
    public async Task Conta_inexistente_ou_campos_ausentes_retornam_422(string body)
    {
        await AssertProblem(await PostAsync(body), HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10.999")]
    [InlineData("99999999999999999")]
    public async Task Valor_invalido_retorna_422(string amount)
    {
        var accountId = await SeedAccountAsync();

        await AssertProblem(
            await PostAsync($$"""{"accountId":{{accountId}},"amount":{{amount}},"realizedAt":"2026-10-06"}"""),
            HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Descricao_acima_de_500_retorna_422()
    {
        var accountId = await SeedAccountAsync();

        await AssertProblem(
            await PostAsync($$"""{"accountId":{{accountId}},"amount":10,"realizedAt":"2026-10-06","description":"{{new string('a', 501)}}"}"""),
            HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("""{"accountId":1,"amount":""")]
    [InlineData("""{"accountId":"x","amount":10,"realizedAt":"2026-10-06"}""")]
    [InlineData("""{"accountId":1,"amount":"dez","realizedAt":"2026-10-06"}""")]
    [InlineData("""{"accountId":1,"amount":10,"realizedAt":"2026-13-45"}""")]
    [InlineData("""{"accountId":1,"amount":10,"realizedAt":"hoje"}""")]
    [InlineData("""{"accountId":1,"amount":10,"realizedAt":"2026-10-06","natureza":"X"}""")]
    [InlineData("")]
    public async Task Sintaxe_tipo_propriedade_desconhecida_ou_corpo_vazio_retornam_400(string body)
    {
        await AssertProblem(await PostAsync(body), HttpStatusCode.BadRequest);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }
}
