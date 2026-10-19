using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Tests.Http;

// Smoke de fechamento da feature Contas: as 6 operações em um fluxo de ponta a ponta.
public sealed class AccountsClosureContractTests(SqlServerDatabaseFixture database)
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
    public async Task Fluxo_completo_das_seis_operacoes()
    {
        var client = _factory.CreateClient();

        var category = await client.PostAsJsonAsync("/api/v1/categories", new { name = "Smoke Contas", nature = "EXPENSE" });
        using var categoryJson = JsonDocument.Parse(await category.Content.ReadAsStringAsync());
        var categoryId = categoryJson.RootElement.GetProperty("id").GetInt32();

        var created = await client.PostAsJsonAsync("/api/v1/accounts", new { categoryId, name = "Conta Smoke", classifier = "OF" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdJson.RootElement.GetProperty("id").GetInt32();
        Assert.EndsWith($"/api/v1/accounts/{id}", created.Headers.Location!.ToString());

        Assert.Contains("\"Conta Smoke\"", await client.GetStringAsync($"/api/v1/accounts?categoryId={categoryId}"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/accounts/{id}")).StatusCode);

        var patched = await client.PatchAsJsonAsync($"/api/v1/accounts/{id}", new { name = "Conta Smoke 2" });
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        Assert.Contains("Conta Smoke 2", await patched.Content.ReadAsStringAsync());

        var status = await client.PatchAsJsonAsync($"/api/v1/accounts/{id}/status", new { active = false });
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var statusJson = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        Assert.False(statusJson.RootElement.GetProperty("active").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/accounts/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/accounts/{id}")).StatusCode);
    }

    [Fact]
    public void Openapi_declara_as_seis_operacoes_de_contas_implementadas()
    {
        var yaml = File.ReadAllText(Path.Combine(
            Architecture.ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var endpoints = _factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .Select(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.EndpointNameMetadata>()?.EndpointName)
            .Where(n => n is not null)
            .ToHashSet();

        foreach (var operation in new[] { "createAccount", "listAccounts", "getAccount", "updateAccount", "changeAccountStatus", "deleteAccount" })
        {
            Assert.Contains($"operationId: {operation}", yaml);
            Assert.Contains(operation, endpoints);
        }
    }
}
