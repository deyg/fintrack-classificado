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

// Contrato updateCategory (PATCH /api/v1/categories/{id}).
public sealed class UpdateCategoryContractTests(SqlServerDatabaseFixture database)
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

    private async Task<int> SeedAsync(string name, CategoryNature nature, bool withAccount = false)
    {
        await using var context = database.CreateContext();
        var category = Category.Create(name, nature);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        if (withAccount)
        {
            context.Accounts.Add(Account.Create(category.Id, "Conta " + name, nature, nature == CategoryNature.Expense ? ExpenseClassifier.OV : null));
            await context.SaveChangesAsync();
        }

        return category.Id;
    }

    private Task<HttpResponseMessage> PatchAsync(string id, string body) =>
        _factory.CreateClient().PatchAsync($"/api/v1/categories/{id}",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

    [Fact]
    public void Contrato_openapi_declara_updateCategory_com_200_400_404_409_422()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: updateCategory", StringComparison.Ordinal);
        Assert.True(start > 0);
        var end = yaml.IndexOf("    delete:", start, StringComparison.Ordinal);
        var block = yaml[start..end];

        foreach (var code in new[] { "200", "400", "404", "409", "422" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Renomeia_sem_vinculos_retorna_200_e_persiste()
    {
        var id = await SeedAsync("Antigo", CategoryNature.Expense);

        var response = await PatchAsync(id.ToString(), """{"name":" Novo "}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Novo", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("EXPENSE", json.RootElement.GetProperty("nature").GetString());
        await using var context = database.CreateContext();
        Assert.Equal("Novo", (await context.Categories.AsNoTracking().SingleAsync(c => c.Id == id)).Name);
    }

    [Fact]
    public async Task Altera_natureza_sem_vinculos_retorna_200_e_persiste()
    {
        var id = await SeedAsync("Extra", CategoryNature.Expense);

        var response = await PatchAsync(id.ToString(), """{"nature":"INCOME"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = database.CreateContext();
        Assert.Equal(CategoryNature.Income, (await context.Categories.AsNoTracking().SingleAsync(c => c.Id == id)).Nature);
    }

    [Fact]
    public async Task Sem_mudanca_efetiva_com_conta_vinculada_retorna_200()
    {
        var id = await SeedAsync("Fixa", CategoryNature.Expense, withAccount: true);

        var response = await PatchAsync(id.ToString(), """{"name":"Fixa","nature":"EXPENSE"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"name":"Outro"}""")]
    [InlineData("""{"nature":"INCOME"}""")]
    public async Task Com_conta_vinculada_renomear_ou_mudar_natureza_retorna_409_e_nao_altera(string body)
    {
        var id = await SeedAsync("Vinculada" + Guid.NewGuid().ToString("N")[..6], CategoryNature.Expense, withAccount: true);
        await using var before = database.CreateContext();
        var original = await before.Categories.AsNoTracking().SingleAsync(c => c.Id == id);

        var response = await PatchAsync(id.ToString(), body);

        await AssertProblem(response, HttpStatusCode.Conflict);
        await using var after = database.CreateContext();
        var current = await after.Categories.AsNoTracking().SingleAsync(c => c.Id == id);
        Assert.Equal(original.Name, current.Name);
        Assert.Equal(original.Nature, current.Nature);
    }

    [Fact]
    public async Task Inexistente_retorna_404()
    {
        var response = await PatchAsync("999999", """{"name":"X"}""");

        await AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"name":""}""")]
    [InlineData("""{"name":"   "}""")]
    [InlineData("""{"nature":"RECEITA"}""")]
    public async Task Valores_invalidos_retornam_422(string body)
    {
        var id = await SeedAsync("Valida" + Guid.NewGuid().ToString("N")[..6], CategoryNature.Expense);

        var response = await PatchAsync(id.ToString(), body);

        await AssertProblem(response, HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("1", """{"name":""" )]
    [InlineData("1", """{"name":5}""")]
    [InlineData("1", """{"name":"X","extra":1}""")]
    [InlineData("0", """{"name":"X"}""")]
    [InlineData("abc", """{"name":"X"}""")]
    public async Task Sintaxe_tipo_propriedade_desconhecida_ou_id_invalido_retornam_400(string id, string body)
    {
        var response = await PatchAsync(id, body);

        await AssertProblem(response, HttpStatusCode.BadRequest);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal($"https://httpstatuses.com/{(int)status}", json.RootElement.GetProperty("type").GetString());
    }
}
