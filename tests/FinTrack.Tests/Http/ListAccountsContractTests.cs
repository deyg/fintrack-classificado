using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Tests.Http;

// Contrato listAccounts (GET /api/v1/accounts). Banco novo por teste para listas determinísticas.
public sealed class ListAccountsContractTests : IAsyncLifetime
{
    private const string Route = "/api/v1/accounts";
    private readonly SqlServerDatabaseFixture _database = new();
    private WebApplicationFactory<Program> _factory = null!;
    private int _expenseCategory;
    private int _incomeCategory;

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
        var expense = Category.Create("Despesas", CategoryNature.Expense);
        var income = Category.Create("Receitas", CategoryNature.Income);
        context.Categories.AddRange(expense, income);
        await context.SaveChangesAsync();
        _expenseCategory = expense.Id;
        _incomeCategory = income.Id;

        var inactive = Account.Create(expense.Id, "Inativa", CategoryNature.Expense, ExpenseClassifier.OV);
        inactive.Deactivate();
        context.Accounts.AddRange(
            Account.Create(expense.Id, "Aluguel", CategoryNature.Expense, ExpenseClassifier.OF),
            inactive,
            Account.Create(income.Id, "Salario", CategoryNature.Income, null));
        await context.SaveChangesAsync();
    }

    private async Task<string[]> NamesAsync(string query)
    {
        var response = await _factory.CreateClient().GetAsync(Route + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        return items!.Select(i => i.GetProperty("name").GetString()!).ToArray();
    }

    [Fact]
    public void Contrato_openapi_declara_listAccounts_com_filtros_200_e_400()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: listAccounts", StringComparison.Ordinal);
        var block = yaml[start..yaml.IndexOf("    post:", start, StringComparison.Ordinal)];

        foreach (var parameter in new[] { "categoryId", "active", "classifier" })
        {
            Assert.Contains($"name: {parameter}", block);
        }

        Assert.Contains("'200':", block);
        Assert.Contains("'400':", block);
    }

    [Fact]
    public async Task Sem_contas_retorna_lista_vazia()
    {
        Assert.Empty(await NamesAsync(""));
    }

    [Fact]
    public async Task Sem_filtros_retorna_todas_ordenadas_por_id_com_o_schema_Account()
    {
        await SeedAsync();

        Assert.Equal(["Aluguel", "Inativa", "Salario"], await NamesAsync(""));

        var items = await _factory.CreateClient().GetFromJsonAsync<JsonElement[]>(Route);
        Assert.Equal(["active", "categoryId", "classifier", "id", "name"], items![0].EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(JsonValueKind.Null, items[2].GetProperty("classifier").ValueKind);
    }

    [Fact]
    public async Task Filtra_por_categoria_status_e_classificador_e_combina_com_and()
    {
        await SeedAsync();

        Assert.Equal(["Salario"], await NamesAsync($"?categoryId={_incomeCategory}"));
        Assert.Equal(["Aluguel", "Inativa"], await NamesAsync($"?categoryId={_expenseCategory}"));
        Assert.Equal(["Aluguel", "Salario"], await NamesAsync("?active=true"));
        Assert.Equal(["Inativa"], await NamesAsync("?active=false"));
        Assert.Equal(["Inativa"], await NamesAsync("?classifier=OV"));
        Assert.Equal(["Inativa"], await NamesAsync($"?categoryId={_expenseCategory}&active=false&classifier=OV"));
        Assert.Empty(await NamesAsync($"?categoryId={_incomeCategory}&classifier=OF"));
        Assert.Empty(await NamesAsync("?categoryId=999999"));
    }

    [Theory]
    [InlineData("?categoryId=0")]
    [InlineData("?categoryId=-1")]
    [InlineData("?categoryId=abc")]
    [InlineData("?active=talvez")]
    [InlineData("?classifier=ZZ")]
    [InlineData("?classifier=of")]
    [InlineData("?classifier=")]
    public async Task Parametros_invalidos_retornam_400_problem_details(string query)
    {
        var response = await _factory.CreateClient().GetAsync(Route + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
    }
}
