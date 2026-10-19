using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Tests.Http;

// Fechamento da feature Lançamentos: as 6 operações em fluxo único e as regressões de Conta com lançamentos.
public sealed class TransactionsClosureContractTests(SqlServerDatabaseFixture database)
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

    private static async Task<int> IdOf(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<int> CreateAccountAsync(HttpClient client, string name = "Conta Fechamento")
    {
        var category = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/categories", new { name = "Cat " + Guid.NewGuid().ToString("N")[..8], nature = "EXPENSE" }));
        return await IdOf(await client.PostAsJsonAsync("/api/v1/accounts", new { categoryId = category, name, classifier = "OF" }));
    }

    private static string Today => DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Fluxo_completo_das_seis_operacoes()
    {
        var client = _factory.CreateClient();
        var accountId = await CreateAccountAsync(client);

        var created = await client.PostAsJsonAsync(
            "/api/v1/transactions", new { accountId, amount = 99.90m, realizedAt = "2026-01-15", description = "Smoke" });
        var id = await IdOf(created);
        Assert.EndsWith($"/api/v1/transactions/{id}", created.Headers.Location!.ToString());

        Assert.Contains("Smoke", await client.GetStringAsync($"/api/v1/transactions?accountId={accountId}"));
        Assert.Contains("Smoke", await client.GetStringAsync($"/api/v1/accounts/{accountId}/transactions"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/transactions/{id}")).StatusCode);

        var patched = await client.PatchAsJsonAsync($"/api/v1/transactions/{id}", new { amount = 120.5m });
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        Assert.Contains("120.5", await patched.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/transactions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/transactions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/transactions/{id}")).StatusCode);
    }

    [Fact]
    public async Task Conta_com_lancamento_nao_pode_ser_excluida_e_volta_a_poder_apos_excluir_o_lancamento()
    {
        var client = _factory.CreateClient();
        var accountId = await CreateAccountAsync(client);
        var id = await IdOf(await client.PostAsJsonAsync("/api/v1/transactions", new { accountId, amount = 10, realizedAt = Today }));

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/v1/accounts/{accountId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/accounts/{accountId}")).StatusCode);

        await client.DeleteAsync($"/api/v1/transactions/{id}");

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/accounts/{accountId}")).StatusCode);
    }

    [Fact]
    public async Task Conta_com_lancamento_nao_muda_categoria_nem_classificador_mas_aceita_novo_nome()
    {
        var client = _factory.CreateClient();
        var accountId = await CreateAccountAsync(client);
        var otherCategory = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/categories", new { name = "Outra " + Guid.NewGuid().ToString("N")[..8], nature = "EXPENSE" }));
        await IdOf(await client.PostAsJsonAsync("/api/v1/transactions", new { accountId, amount = 10, realizedAt = Today }));

        var changeCategory = await client.PatchAsJsonAsync($"/api/v1/accounts/{accountId}", new { categoryId = otherCategory });
        var changeClassifier = await client.PatchAsJsonAsync($"/api/v1/accounts/{accountId}", new { classifier = "OV" });
        var rename = await client.PatchAsJsonAsync($"/api/v1/accounts/{accountId}", new { name = "Renomeada" });

        Assert.Equal(HttpStatusCode.Conflict, changeCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changeClassifier.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    }

    [Fact]
    public async Task Conta_desativada_rejeita_novo_lancamento_mas_preserva_e_lista_os_existentes()
    {
        var client = _factory.CreateClient();
        var accountId = await CreateAccountAsync(client);
        await IdOf(await client.PostAsJsonAsync("/api/v1/transactions", new { accountId, amount = 10, realizedAt = Today }));

        await client.PatchAsJsonAsync($"/api/v1/accounts/{accountId}/status", new { active = false });
        var rejected = await client.PostAsJsonAsync("/api/v1/transactions", new { accountId, amount = 5, realizedAt = Today });

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/accounts/{accountId}/transactions"));
        Assert.Equal(1, json.RootElement.GetProperty("pagination").GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public void Openapi_declara_as_operacoes_de_lancamentos_implementadas()
    {
        var yaml = File.ReadAllText(Path.Combine(
            Architecture.ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var endpoints = _factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .Select(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.EndpointNameMetadata>()?.EndpointName)
            .Where(n => n is not null)
            .ToHashSet();

        foreach (var operation in new[]
        {
            "createTransaction", "listTransactions", "getTransaction", "updateTransaction", "deleteTransaction", "listAccountTransactions",
        })
        {
            Assert.Contains($"operationId: {operation}", yaml);
            Assert.Contains(operation, endpoints);
        }
    }
}
