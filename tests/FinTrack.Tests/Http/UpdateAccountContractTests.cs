using System.Net;
using System.Text;
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

// Contrato updateAccount (PATCH /api/v1/accounts/{id}).
public sealed class UpdateAccountContractTests(SqlServerDatabaseFixture database)
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

    private async Task<int> SeedCategoryAsync(CategoryNature nature)
    {
        await using var context = database.CreateContext();
        var category = Category.Create("Cat " + Guid.NewGuid().ToString("N")[..8], nature);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private async Task<int> SeedAccountAsync(int categoryId, CategoryNature nature, ExpenseClassifier? classifier)
    {
        await using var context = database.CreateContext();
        var account = Account.Create(categoryId, "Conta", nature, classifier);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private static Task<HttpResponseMessage> PatchAsync(WebApplicationFactory<Program> factory, string id, string body) =>
        factory.CreateClient().PatchAsync($"/api/v1/accounts/{id}", new StringContent(body, Encoding.UTF8, "application/json"));

    private async Task<Account> LoadAsync(int id)
    {
        await using var context = database.CreateContext();
        return await context.Accounts.AsNoTracking().SingleAsync(a => a.Id == id);
    }

    [Fact]
    public void Contrato_openapi_declara_updateAccount_com_200_400_404_409_422()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: updateAccount", StringComparison.Ordinal);
        Assert.True(start > 0);
        var block = yaml[start..yaml.IndexOf("  /accounts/{id}/status:", start, StringComparison.Ordinal)];

        foreach (var code in new[] { "200", "400", "404", "409", "422" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Renomeia_e_nao_altera_active()
    {
        var categoryId = await SeedCategoryAsync(CategoryNature.Expense);
        var id = await SeedAccountAsync(categoryId, CategoryNature.Expense, ExpenseClassifier.OF);

        var response = await PatchAsync(_factory, id.ToString(), """{"name":" Casa "}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Casa", json.RootElement.GetProperty("name").GetString());
        Assert.True(json.RootElement.GetProperty("active").GetBoolean());
        Assert.Equal("Casa", (await LoadAsync(id)).Name);
    }

    [Fact]
    public async Task Sem_lancamentos_muda_classificador_e_categoria_para_receita()
    {
        var expense = await SeedCategoryAsync(CategoryNature.Expense);
        var income = await SeedCategoryAsync(CategoryNature.Income);
        var id = await SeedAccountAsync(expense, CategoryNature.Expense, ExpenseClassifier.OF);

        var classifierChange = await PatchAsync(_factory, id.ToString(), """{"classifier":"NOV"}""");
        var toIncome = await PatchAsync(_factory, id.ToString(), $$"""{"categoryId":{{income}},"classifier":null}""");

        Assert.Equal(HttpStatusCode.OK, classifierChange.StatusCode);
        Assert.Equal(HttpStatusCode.OK, toIncome.StatusCode);
        var stored = await LoadAsync(id);
        Assert.Equal(income, stored.CategoryId);
        Assert.Null(stored.Classifier);
    }

    [Fact]
    public async Task Categoria_incoerente_com_classificador_retorna_422_e_nao_altera()
    {
        var expense = await SeedCategoryAsync(CategoryNature.Expense);
        var income = await SeedCategoryAsync(CategoryNature.Income);
        var id = await SeedAccountAsync(expense, CategoryNature.Expense, ExpenseClassifier.OF);

        await AssertProblem(await PatchAsync(_factory, id.ToString(), $$"""{"categoryId":{{income}}}"""), HttpStatusCode.UnprocessableEntity, "classifier");
        await AssertProblem(await PatchAsync(_factory, id.ToString(), """{"classifier":null}"""), HttpStatusCode.UnprocessableEntity, "classifier");
        var stored = await LoadAsync(id);
        Assert.Equal(expense, stored.CategoryId);
        Assert.Equal(ExpenseClassifier.OF, stored.Classifier);
    }

    [Fact]
    public async Task Categoria_destino_inexistente_retorna_422()
    {
        var expense = await SeedCategoryAsync(CategoryNature.Expense);
        var id = await SeedAccountAsync(expense, CategoryNature.Expense, ExpenseClassifier.OF);

        await AssertProblem(await PatchAsync(_factory, id.ToString(), """{"categoryId":999999}"""), HttpStatusCode.UnprocessableEntity, "categoryId");
    }

    [Theory]
    [InlineData("""{"categoryId":999}""")]
    [InlineData("""{"classifier":"OV"}""")]
    public async Task Com_lancamentos_reclassificar_retorna_409_e_nao_altera(string body)
    {
        var expense = await SeedCategoryAsync(CategoryNature.Expense);
        var other = await SeedCategoryAsync(CategoryNature.Expense);
        var id = await SeedAccountAsync(expense, CategoryNature.Expense, ExpenseClassifier.OF);
        body = body.Replace("999", other.ToString());

        await AssertProblem(await PatchAsync(_withHistory, id.ToString(), body), HttpStatusCode.Conflict, null);

        var stored = await LoadAsync(id);
        Assert.Equal(expense, stored.CategoryId);
        Assert.Equal(ExpenseClassifier.OF, stored.Classifier);
    }

    [Fact]
    public async Task Com_lancamentos_renomear_continua_permitido()
    {
        var expense = await SeedCategoryAsync(CategoryNature.Expense);
        var id = await SeedAccountAsync(expense, CategoryNature.Expense, ExpenseClassifier.OF);

        var response = await PatchAsync(_withHistory, id.ToString(), """{"name":"Outra"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Outra", (await LoadAsync(id)).Name);
    }

    [Fact]
    public async Task Inexistente_retorna_404()
    {
        await AssertProblem(await PatchAsync(_factory, "999999", """{"name":"X"}"""), HttpStatusCode.NotFound, null);
    }

    [Theory]
    [InlineData("""{}""", "body")]
    [InlineData("""{"name":""}""", "name")]
    [InlineData("""{"name":"   "}""", "name")]
    [InlineData("""{"categoryId":0}""", "categoryId")]
    [InlineData("""{"classifier":"of"}""", "classifier")]
    public async Task Valores_invalidos_retornam_422(string body, string field)
    {
        var categoryId = await SeedCategoryAsync(CategoryNature.Expense);
        var id = await SeedAccountAsync(categoryId, CategoryNature.Expense, ExpenseClassifier.OF);

        await AssertProblem(await PatchAsync(_factory, id.ToString(), body), HttpStatusCode.UnprocessableEntity, field);
    }

    [Theory]
    [InlineData("1", """{"name":""")]
    [InlineData("1", """{"name":5}""")]
    [InlineData("1", """{"name":"X","active":false}""")]
    [InlineData("0", """{"name":"X"}""")]
    [InlineData("abc", """{"name":"X"}""")]
    public async Task Sintaxe_tipo_propriedade_desconhecida_ou_id_invalido_retornam_400(string id, string body)
    {
        await AssertProblem(await PatchAsync(_factory, id, body), HttpStatusCode.BadRequest, null);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string? field)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        if (field is not null)
        {
            Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        }
    }
}
