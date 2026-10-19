using System.Net;
using System.Text;
using System.Text.Json;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Tests.Http;

// Contrato changeAccountStatus (PATCH /api/v1/accounts/{id}/status).
public sealed class ChangeAccountStatusContractTests(SqlServerDatabaseFixture database)
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

    private async Task<int> SeedAccountAsync()
    {
        await using var context = database.CreateContext();
        var category = Category.Create("Cat " + Guid.NewGuid().ToString("N")[..8], CategoryNature.Expense);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var account = Account.Create(category.Id, "Conta", CategoryNature.Expense, ExpenseClassifier.OF);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private Task<HttpResponseMessage> PatchAsync(string id, string body) =>
        _factory.CreateClient().PatchAsync($"/api/v1/accounts/{id}/status", new StringContent(body, Encoding.UTF8, "application/json"));

    private async Task<bool> StoredActiveAsync(int id)
    {
        await using var context = database.CreateContext();
        return (await context.Accounts.AsNoTracking().SingleAsync(a => a.Id == id)).Active;
    }

    [Fact]
    public void Contrato_openapi_declara_changeAccountStatus_com_200_400_404_422()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: changeAccountStatus", StringComparison.Ordinal);
        Assert.True(start > 0);
        var block = yaml[start..yaml.IndexOf("  /accounts/{id}/transactions:", start, StringComparison.Ordinal)];

        foreach (var code in new[] { "200", "400", "404", "422" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Desativa_reativa_e_repeticao_e_idempotente()
    {
        var id = await SeedAccountAsync();

        var off = await PatchAsync(id.ToString(), """{"active":false}""");
        var offAgain = await PatchAsync(id.ToString(), """{"active":false}""");

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.Equal(HttpStatusCode.OK, offAgain.StatusCode);
        using var json = JsonDocument.Parse(await offAgain.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("active").GetBoolean());
        Assert.False(await StoredActiveAsync(id));

        var on = await PatchAsync(id.ToString(), """{"active":true}""");

        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.True(await StoredActiveAsync(id));
    }

    [Fact]
    public async Task Inexistente_retorna_404()
    {
        await AssertProblem(await PatchAsync("999999", """{"active":true}"""), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"active":null}""")]
    public async Task Active_ausente_retorna_422(string body)
    {
        var id = await SeedAccountAsync();

        await AssertProblem(await PatchAsync(id.ToString(), body), HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("1", """{"active":""")]
    [InlineData("1", """{"active":"sim"}""")]
    [InlineData("1", """{"active":true,"name":"X"}""")]
    [InlineData("0", """{"active":true}""")]
    [InlineData("abc", """{"active":true}""")]
    public async Task Sintaxe_tipo_propriedade_desconhecida_ou_id_invalido_retornam_400(string id, string body)
    {
        await AssertProblem(await PatchAsync(id, body), HttpStatusCode.BadRequest);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }
}
