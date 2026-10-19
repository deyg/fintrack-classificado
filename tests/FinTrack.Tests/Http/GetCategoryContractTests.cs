using System.Net;
using System.Text.Json;
using FinTrack.Domain.Categories;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Tests.Http;

// Contrato getCategory (GET /api/v1/categories/{id}).
public sealed class GetCategoryContractTests(SqlServerDatabaseFixture database)
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

    private async Task<int> SeedAsync(string name, CategoryNature nature)
    {
        await using var context = database.CreateContext();
        var category = Category.Create(name, nature);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    [Fact]
    public void Contrato_openapi_declara_getCategory_com_200_400_404()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: getCategory", StringComparison.Ordinal);
        var block = yaml[start..yaml.IndexOf("    patch:", start, StringComparison.Ordinal)];

        Assert.Contains("'200':", block);
        Assert.Contains("'400':", block);
        Assert.Contains("'404':", block);
    }

    [Fact]
    public async Task Get_existente_retorna_200_no_schema_Category()
    {
        var id = await SeedAsync("Salário", CategoryNature.Income);

        var response = await _factory.CreateClient().GetAsync($"/api/v1/categories/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(id, root.GetProperty("id").GetInt32());
        Assert.Equal("Salário", root.GetProperty("name").GetString());
        Assert.Equal("INCOME", root.GetProperty("nature").GetString());
        Assert.Equal(["id", "name", "nature"], root.EnumerateObject().Select(p => p.Name).Order());
    }

    [Theory]
    [InlineData("999999")]
    [InlineData("2147483647")]
    public async Task Get_inexistente_retorna_404_problem_details(string id)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/v1/categories/{id}");

        await AssertProblem(response, HttpStatusCode.NotFound, $"/api/v1/categories/{id}");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    public async Task Get_com_identificador_invalido_retorna_400_problem_details(string id)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/v1/categories/{id}");

        await AssertProblem(response, HttpStatusCode.BadRequest, instance: null);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string? instance)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.Equal($"https://httpstatuses.com/{(int)status}", root.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        if (instance is not null)
        {
            Assert.Equal(instance, root.GetProperty("instance").GetString());
        }
    }
}
