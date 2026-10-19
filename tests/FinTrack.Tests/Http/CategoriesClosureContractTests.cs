using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Tests.Http;

// Smoke de fechamento da feature Categorias: as 5 operações em um fluxo de ponta a ponta.
public sealed class CategoriesClosureContractTests(SqlServerDatabaseFixture database)
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

    [Fact]
    public async Task Fluxo_crud_completo_das_cinco_operacoes()
    {
        var client = _factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/v1/categories", new { name = "Smoke", nature = "EXPENSE" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location!.ToString();
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("id").GetInt32();
        Assert.EndsWith($"/api/v1/categories/{id}", location);

        var listed = await client.GetStringAsync("/api/v1/categories?nature=EXPENSE");
        Assert.Contains("\"Smoke\"", listed);

        var fetched = await client.GetAsync($"/api/v1/categories/{id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

        var patched = await client.PatchAsJsonAsync($"/api/v1/categories/{id}", new { name = "Smoke 2", nature = "INCOME" });
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        using var patchedJson = JsonDocument.Parse(await patched.Content.ReadAsStringAsync());
        Assert.Equal("Smoke 2", patchedJson.RootElement.GetProperty("name").GetString());
        Assert.Equal("INCOME", patchedJson.RootElement.GetProperty("nature").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/categories/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/categories/{id}")).StatusCode);
    }

    [Fact]
    public void Openapi_declara_as_cinco_operacoes_de_categorias_implementadas()
    {
        var yaml = File.ReadAllText(Path.Combine(
            Architecture.ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var endpoints = _factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .Select(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.EndpointNameMetadata>()?.EndpointName)
            .Where(n => n is not null)
            .ToHashSet();

        foreach (var operation in new[] { "createCategory", "listCategories", "getCategory", "updateCategory", "deleteCategory" })
        {
            Assert.Contains($"operationId: {operation}", yaml);
            Assert.Contains(operation, endpoints);
        }
    }
}
