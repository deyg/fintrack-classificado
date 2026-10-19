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

// Contrato deleteCategory (DELETE /api/v1/categories/{id}).
public sealed class DeleteCategoryContractTests(SqlServerDatabaseFixture database)
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

    private async Task<int> SeedAsync(string name, bool withAccount)
    {
        await using var context = database.CreateContext();
        var category = Category.Create(name, CategoryNature.Expense);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        if (withAccount)
        {
            context.Accounts.Add(Account.Create(category.Id, "Conta " + name, CategoryNature.Expense, ExpenseClassifier.OV));
            await context.SaveChangesAsync();
        }

        return category.Id;
    }

    private Task<HttpResponseMessage> DeleteAsync(string id) => _factory.CreateClient().DeleteAsync($"/api/v1/categories/{id}");

    [Fact]
    public void Contrato_openapi_declara_deleteCategory_com_204_400_404_409()
    {
        var yaml = File.ReadAllText(Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var start = yaml.IndexOf("operationId: deleteCategory", StringComparison.Ordinal);
        Assert.True(start > 0);
        var block = yaml[start..yaml.IndexOf("  /accounts:", start, StringComparison.Ordinal)];

        foreach (var code in new[] { "204", "400", "404", "409" })
        {
            Assert.Contains($"'{code}':", block);
        }
    }

    [Fact]
    public async Task Exclui_sem_contas_retorna_204_sem_corpo_e_remove()
    {
        var id = await SeedAsync("Descartavel", withAccount: false);

        var response = await DeleteAsync(id.ToString());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        await using var context = database.CreateContext();
        Assert.False(await context.Categories.AnyAsync(c => c.Id == id));
        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(id.ToString())).StatusCode);
    }

    [Fact]
    public async Task Com_conta_vinculada_retorna_409_sem_cascata()
    {
        var id = await SeedAsync("Protegida", withAccount: true);

        var response = await DeleteAsync(id.ToString());

        await AssertProblem(response, HttpStatusCode.Conflict);
        await using var context = database.CreateContext();
        Assert.True(await context.Categories.AnyAsync(c => c.Id == id));
        Assert.True(await context.Accounts.AnyAsync(a => a.CategoryId == id));
    }

    [Fact]
    public async Task Fk_restrict_impede_exclusao_direta_de_categoria_com_conta()
    {
        var id = await SeedAsync("FkRestrict", withAccount: true);
        await using var context = database.CreateContext();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Categories.Where(c => c.Id == id).ExecuteDeleteAsync());
    }

    [Fact]
    public async Task Inexistente_retorna_404()
    {
        await AssertProblem(await DeleteAsync("999999"), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task Id_invalido_retorna_400(string id)
    {
        await AssertProblem(await DeleteAsync(id), HttpStatusCode.BadRequest);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
    }
}
