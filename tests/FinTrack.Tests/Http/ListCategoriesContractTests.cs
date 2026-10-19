using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Domain.Categories;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Tests.Http;

// Contrato listCategories (GET /api/v1/categories). Banco novo por teste para listas determinísticas.
public sealed class ListCategoriesContractTests : IAsyncLifetime
{
    private const string Route = "/api/v1/categories";
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
        context.Categories.AddRange(
            Category.Create("Salário", CategoryNature.Income),
            Category.Create("Mercado", CategoryNature.Expense),
            Category.Create("Bônus", CategoryNature.Income));
        await context.SaveChangesAsync();
    }

    private async Task<JsonElement[]> GetListAsync(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        return items!;
    }

    [Fact]
    public void Contrato_openapi_declara_listCategories_com_filtro_nature_200_e_400()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: listCategories", StringComparison.Ordinal);
        var block = yaml[start..yaml.IndexOf("    post:", start, StringComparison.Ordinal)];

        Assert.Contains("name: nature", block);
        Assert.Contains("'200':", block);
        Assert.Contains("'400':", block);
    }

    [Fact]
    public async Task Get_sem_categorias_retorna_200_com_lista_vazia()
    {
        Assert.Empty(await GetListAsync(Route));
    }

    [Fact]
    public async Task Get_retorna_todas_por_id_crescente_no_schema_Category()
    {
        await SeedAsync();

        var items = await GetListAsync(Route);

        Assert.Equal(["Salário", "Mercado", "Bônus"], items.Select(i => i.GetProperty("name").GetString()));
        Assert.Equal(["INCOME", "EXPENSE", "INCOME"], items.Select(i => i.GetProperty("nature").GetString()));
        Assert.All(items, i => Assert.Equal(["id", "name", "nature"], i.EnumerateObject().Select(p => p.Name).Order()));
        Assert.Equal(items.Select(i => i.GetProperty("id").GetInt32()).Order(), items.Select(i => i.GetProperty("id").GetInt32()));
    }

    [Theory]
    [InlineData("INCOME", new[] { "Salário", "Bônus" })]
    [InlineData("EXPENSE", new[] { "Mercado" })]
    public async Task Get_com_filtro_nature_retorna_apenas_a_natureza(string nature, string[] expected)
    {
        await SeedAsync();

        var items = await GetListAsync($"{Route}?nature={nature}");

        Assert.Equal(expected, items.Select(i => i.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Get_com_filtro_sem_resultados_retorna_lista_vazia()
    {
        await using (var context = _database.CreateContext())
        {
            context.Categories.Add(Category.Create("Mercado", CategoryNature.Expense));
            await context.SaveChangesAsync();
        }

        Assert.Empty(await GetListAsync($"{Route}?nature=INCOME"));
    }

    [Theory]
    [InlineData("RECEITA")]
    [InlineData("income")]
    [InlineData("")]
    public async Task Get_com_nature_invalida_retorna_400_problem_details(string nature)
    {
        var response = await _factory.CreateClient().GetAsync($"{Route}?nature={nature}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("https://httpstatuses.com/400", json.RootElement.GetProperty("type").GetString());
        Assert.True(json.RootElement.GetProperty("errors").GetProperty("nature").GetArrayLength() >= 1);
    }
}
