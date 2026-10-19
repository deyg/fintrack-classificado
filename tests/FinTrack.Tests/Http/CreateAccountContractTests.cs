using System.Net;
using System.Text;
using System.Text.Json;
using FinTrack.Domain.Categories;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Tests.Http;

// Contrato createAccount (POST /api/v1/accounts).
public sealed class CreateAccountContractTests(SqlServerDatabaseFixture database)
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

    private async Task<int> SeedCategoryAsync(CategoryNature nature)
    {
        await using var context = database.CreateContext();
        var category = Category.Create("Cat " + Guid.NewGuid().ToString("N")[..8], nature);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private Task<HttpResponseMessage> PostAsync(string body) =>
        _factory.CreateClient().PostAsync("/api/v1/accounts", new StringContent(body, Encoding.UTF8, "application/json"));

    [Fact]
    public void Contrato_openapi_declara_createAccount_com_201_400_422()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: createAccount", StringComparison.Ordinal);
        Assert.True(start > 0);
        var block = yaml[start..yaml.IndexOf("  /accounts/{id}:", start, StringComparison.Ordinal)];

        foreach (var code in new[] { "201", "400", "422" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Despesa_com_classificador_retorna_201_location_e_schema_Account()
    {
        var categoryId = await SeedCategoryAsync(CategoryNature.Expense);

        var response = await PostAsync($$"""{"categoryId":{{categoryId}},"name":" Aluguel ","classifier":"OF"}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var id = root.GetProperty("id").GetInt32();
        Assert.Equal($"/api/v1/accounts/{id}", response.Headers.Location!.OriginalString);
        Assert.Equal(categoryId, root.GetProperty("categoryId").GetInt32());
        Assert.Equal("Aluguel", root.GetProperty("name").GetString());
        Assert.Equal("OF", root.GetProperty("classifier").GetString());
        Assert.True(root.GetProperty("active").GetBoolean());
        Assert.Equal(["active", "categoryId", "classifier", "id", "name"], root.EnumerateObject().Select(p => p.Name).Order());
    }

    [Theory]
    [InlineData("""{"name":"Salario"}""")]
    [InlineData("""{"name":"Salario","classifier":null}""")]
    public async Task Receita_sem_classificador_retorna_201_com_classifier_nulo(string bodyWithoutCategory)
    {
        var categoryId = await SeedCategoryAsync(CategoryNature.Income);
        var body = bodyWithoutCategory.Replace("{", $$"""{"categoryId":{{categoryId}},""");

        var response = await PostAsync(body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("classifier").ValueKind);
    }

    [Fact]
    public async Task Despesa_sem_classificador_retorna_422()
    {
        var categoryId = await SeedCategoryAsync(CategoryNature.Expense);

        await AssertProblem(await PostAsync($$"""{"categoryId":{{categoryId}},"name":"X"}"""), HttpStatusCode.UnprocessableEntity, "classifier");
    }

    [Fact]
    public async Task Receita_com_classificador_retorna_422()
    {
        var categoryId = await SeedCategoryAsync(CategoryNature.Income);

        await AssertProblem(await PostAsync($$"""{"categoryId":{{categoryId}},"name":"X","classifier":"OV"}"""), HttpStatusCode.UnprocessableEntity, "classifier");
    }

    [Fact]
    public async Task Categoria_inexistente_retorna_422()
    {
        await AssertProblem(await PostAsync("""{"categoryId":999999,"name":"X","classifier":"OF"}"""), HttpStatusCode.UnprocessableEntity, "categoryId");
    }

    [Theory]
    [InlineData("""{"name":"X","classifier":"OF"}""", "categoryId")]
    [InlineData("""{"categoryId":0,"name":"X","classifier":"OF"}""", "categoryId")]
    [InlineData("""{"categoryId":1,"classifier":"OF"}""", "name")]
    [InlineData("""{"categoryId":1,"name":"  ","classifier":"OF"}""", "name")]
    [InlineData("""{"categoryId":1,"name":"X","classifier":"of"}""", "classifier")]
    public async Task Valores_invalidos_retornam_422(string body, string field)
    {
        await AssertProblem(await PostAsync(body), HttpStatusCode.UnprocessableEntity, field);
    }

    [Theory]
    [InlineData("""{"categoryId":""")]
    [InlineData("""{"categoryId":"abc","name":"X"}""")]
    [InlineData("""{"categoryId":1,"name":5}""")]
    [InlineData("""{"categoryId":1,"name":"X","extra":1}""")]
    public async Task Sintaxe_tipo_ou_propriedade_desconhecida_retornam_400(string body)
    {
        await AssertProblem(await PostAsync(body), HttpStatusCode.BadRequest, null);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string? field)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal($"https://httpstatuses.com/{(int)status}", json.RootElement.GetProperty("type").GetString());
        if (field is not null)
        {
            Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        }
    }
}
