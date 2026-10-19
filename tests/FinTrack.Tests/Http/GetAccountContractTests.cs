using System.Net;
using System.Text.Json;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Tests.Http;

// Contrato getAccount (GET /api/v1/accounts/{id}).
public sealed class GetAccountContractTests(SqlServerDatabaseFixture database)
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

    private async Task<(int CategoryId, int AccountId)> SeedAsync(CategoryNature nature, ExpenseClassifier? classifier, bool active = true)
    {
        await using var context = database.CreateContext();
        var category = Category.Create("Cat " + Guid.NewGuid().ToString("N")[..8], nature);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var account = Account.Create(category.Id, "Conta", nature, classifier);
        if (!active)
        {
            account.Deactivate();
        }

        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return (category.Id, account.Id);
    }

    [Fact]
    public void Contrato_openapi_declara_getAccount_com_200_400_404()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: getAccount", StringComparison.Ordinal);
        var block = yaml[start..yaml.IndexOf("    patch:", start, StringComparison.Ordinal)];

        Assert.Contains("'200':", block);
        Assert.Contains("'400':", block);
        Assert.Contains("'404':", block);
    }

    [Fact]
    public async Task Despesa_retorna_200_com_todos_os_campos()
    {
        var (categoryId, accountId) = await SeedAsync(CategoryNature.Expense, ExpenseClassifier.NOF, active: false);

        var response = await _factory.CreateClient().GetAsync($"/api/v1/accounts/{accountId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(accountId, root.GetProperty("id").GetInt32());
        Assert.Equal(categoryId, root.GetProperty("categoryId").GetInt32());
        Assert.Equal("Conta", root.GetProperty("name").GetString());
        Assert.Equal("NOF", root.GetProperty("classifier").GetString());
        Assert.False(root.GetProperty("active").GetBoolean());
        Assert.Equal(["active", "categoryId", "classifier", "id", "name"], root.EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Receita_retorna_classifier_nulo()
    {
        var (_, accountId) = await SeedAsync(CategoryNature.Income, null);

        var response = await _factory.CreateClient().GetAsync($"/api/v1/accounts/{accountId}");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("classifier").ValueKind);
        Assert.True(json.RootElement.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task Inexistente_retorna_404_com_instance()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/accounts/999999");

        await AssertProblem(response, HttpStatusCode.NotFound);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("/api/v1/accounts/999999", json.RootElement.GetProperty("instance").GetString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("2147483648")]
    public async Task Identificador_invalido_retorna_400(string id)
    {
        await AssertProblem(await _factory.CreateClient().GetAsync($"/api/v1/accounts/{id}"), HttpStatusCode.BadRequest);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }
}
