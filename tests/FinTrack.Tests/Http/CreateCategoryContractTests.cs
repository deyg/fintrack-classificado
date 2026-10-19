using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Tests.Http;

// Contrato createCategory (POST /api/v1/categories) com banco SQL Server real isolado. DEC-ERR-005: 400 sintaxe, 422 valores.
public sealed class CreateCategoryContractTests(SqlServerDatabaseFixture database)
    : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string Route = "/api/v1/categories";
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

    [Fact]
    public void Contrato_openapi_declara_createCategory_com_201_400_422()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: createCategory", StringComparison.Ordinal);
        Assert.True(start > 0);
        var block = yaml[start..yaml.IndexOf("  /categories/{id}:", start, StringComparison.Ordinal)];

        Assert.Contains("'201':", block);
        Assert.Contains("'400':", block);
        Assert.Contains("'422':", block);
    }

    [Theory]
    [InlineData("Moradia", "EXPENSE")]
    [InlineData("Salário", "INCOME")]
    public async Task Post_valido_retorna_201_location_e_corpo_do_schema_Category(string name, string nature)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, new { name, nature });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var id = root.GetProperty("id").GetInt32();
        Assert.True(id >= 1);
        Assert.Equal(name, root.GetProperty("name").GetString());
        Assert.Equal(nature, root.GetProperty("nature").GetString());
        Assert.Equal(["id", "name", "nature"], root.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal($"/api/v1/categories/{id}", response.Headers.Location?.ToString());

        await using var context = database.CreateContext();
        Assert.True(await context.Categories.AnyAsync(c => c.Id == id && c.Name == name));
    }

    [Fact]
    public async Task Post_remove_espacos_nas_extremidades_do_nome()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(Route, new { name = "  Lazer  ", nature = "EXPENSE" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Lazer", json.RootElement.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("""{"name":"","nature":"EXPENSE"}""", "name")]
    [InlineData("""{"name":"   ","nature":"EXPENSE"}""", "name")]
    [InlineData("""{"nature":"EXPENSE"}""", "name")]
    [InlineData("""{"name":null,"nature":"EXPENSE"}""", "name")]
    [InlineData("""{"name":"Ok","nature":"RECEITA"}""", "nature")]
    [InlineData("""{"name":"Ok","nature":"income"}""", "nature")]
    [InlineData("""{"name":"Ok"}""", "nature")]
    public async Task Post_com_valores_invalidos_retorna_422_problem_details_com_erros_por_campo(string body, string field)
    {
        var response = await Post(body);

        await AssertProblem(response, HttpStatusCode.UnprocessableEntity, field);
    }

    [Fact]
    public async Task Post_com_nome_de_101_caracteres_retorna_422_e_com_100_retorna_201()
    {
        var tooLong = await Post(JsonSerializer.Serialize(new { name = new string('a', 101), nature = "INCOME" }));
        var limit = await Post(JsonSerializer.Serialize(new { name = new string('b', 100), nature = "INCOME" }));

        await AssertProblem(tooLong, HttpStatusCode.UnprocessableEntity, "name");
        Assert.Equal(HttpStatusCode.Created, limit.StatusCode);
    }

    [Fact]
    public async Task Post_invalido_nao_persiste_nada()
    {
        await Post("""{"name":"NaoDeveExistir","nature":"RECEITA"}""");

        await using var context = database.CreateContext();
        Assert.False(await context.Categories.AnyAsync(c => c.Name == "NaoDeveExistir"));
    }

    [Theory]
    [InlineData("{ nome malformado")]
    [InlineData("")]
    [InlineData("""{"name":123,"nature":"EXPENSE"}""")]
    [InlineData("""{"name":"Ok","nature":"EXPENSE","extra":true}""")]
    [InlineData("null")]
    public async Task Post_com_sintaxe_invalida_retorna_400_problem_details(string body)
    {
        var response = await Post(body);

        await AssertProblem(response, HttpStatusCode.BadRequest, field: null);
    }

    private Task<HttpResponseMessage> Post(string body) =>
        _factory.CreateClient().PostAsync(Route, new StringContent(body, Encoding.UTF8, "application/json"));

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string? field)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.Equal($"https://httpstatuses.com/{(int)status}", root.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));

        if (field is not null)
        {
            var messages = root.GetProperty("errors").GetProperty(field);
            Assert.True(messages.GetArrayLength() >= 1);
        }
    }
}
