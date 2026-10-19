using System.Net;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Tests.Http;

public sealed class ReportByCategoryContractTests : ReportContractTestBase
{
    protected override string Path => "by-category";

    [Fact]
    public void Contrato_openapi_declara_getReportByCategory_com_200_400_422() =>
        AssertOpenApiDeclares("getReportByCategory", "  /reports/by-account:");

    [Fact]
    public async Task Agrupa_por_categoria_receitas_e_despesas_ordenado_por_nome()
    {
        var ids = await SeedAsync();

        var (status, json, _) = await GetAsync("?from=2026-10-01&to=2026-10-31");
        using var doc = json;

        Assert.Equal(HttpStatusCode.OK, status);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);

        Assert.Equal(ids.Housing, items[0].GetProperty("categoryId").GetInt32());
        Assert.Equal("Moradia", items[0].GetProperty("categoryName").GetString());
        Assert.Equal("EXPENSE", items[0].GetProperty("nature").GetString());
        Assert.Equal(2130.50m, items[0].GetProperty("total").GetDecimal());
        Assert.Equal(3, items[0].GetProperty("transactionCount").GetInt32());

        Assert.Equal("Salario", items[1].GetProperty("categoryName").GetString());
        Assert.Equal("INCOME", items[1].GetProperty("nature").GetString());
        Assert.Equal(5000m, items[1].GetProperty("total").GetDecimal());
        Assert.Equal(1, items[1].GetProperty("transactionCount").GetInt32());
        Assert.Equal("2026-10-01", json.RootElement.GetProperty("period").GetProperty("from").GetString());
    }

    [Fact]
    public async Task Periodo_sem_lancamentos_retorna_lista_vazia()
    {
        await SeedAsync();

        var (status, json, _) = await GetAsync("?from=2025-01-01&to=2025-01-31");
        using var doc = json;

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Periodo_de_um_dia_respeita_limites_inclusivos()
    {
        await SeedAsync();

        var (_, json, _) = await GetAsync("?from=2026-10-31&to=2026-10-31");
        using var doc = json;

        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(80m, item.GetProperty("total").GetDecimal());
        Assert.Equal(1, item.GetProperty("transactionCount").GetInt32());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-10-01")]
    [InlineData("?from=2026-10-01&to=31-10-2026")]
    public Task Periodo_ausente_ou_malformado_retorna_400(string query) =>
        AssertInvalidPeriodAsync(query, HttpStatusCode.BadRequest);

    [Fact]
    public Task Periodo_invertido_retorna_422() =>
        AssertInvalidPeriodAsync("?from=2026-11-01&to=2026-10-31", HttpStatusCode.UnprocessableEntity, "period");

    [Fact]
    public async Task Consulta_nao_altera_os_lancamentos()
    {
        await SeedAsync();
        var before = await CountTransactionsAsync();

        var (_, json, _) = await GetAsync("?from=2026-10-01&to=2026-10-31");
        json.Dispose();

        Assert.Equal(before, await CountTransactionsAsync());
    }
}
