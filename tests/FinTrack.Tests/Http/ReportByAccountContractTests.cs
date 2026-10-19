using System.Net;

namespace FinTrack.Tests.Http;

public sealed class ReportByAccountContractTests : ReportContractTestBase
{
    protected override string Path => "by-account";

    [Fact]
    public void Contrato_openapi_declara_getReportByAccount_com_200_400_422() =>
        AssertOpenApiDeclares("getReportByAccount", "  /reports/by-classifier:");

    [Fact]
    public async Task Agrupa_por_conta_com_classificador_e_conta_de_receita_sem_classificador()
    {
        var ids = await SeedAsync();

        var (status, json, _) = await GetAsync("?from=2026-10-01&to=2026-10-31");
        using var doc = json;

        Assert.Equal(HttpStatusCode.OK, status);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(["Aluguel", "Mercado", "Salario"], items.Select(i => i.GetProperty("accountName").GetString()));

        var rent = items[0];
        Assert.Equal(ids.Rent, rent.GetProperty("accountId").GetInt32());
        Assert.Equal(ids.Housing, rent.GetProperty("categoryId").GetInt32());
        Assert.Equal("EXPENSE", rent.GetProperty("nature").GetString());
        Assert.Equal("OF", rent.GetProperty("classifier").GetString());
        Assert.Equal(1800m, rent.GetProperty("total").GetDecimal());
        Assert.Equal(1, rent.GetProperty("transactionCount").GetInt32());

        var market = items[1];
        Assert.Equal("NOV", market.GetProperty("classifier").GetString());
        Assert.Equal(330.50m, market.GetProperty("total").GetDecimal());
        Assert.Equal(2, market.GetProperty("transactionCount").GetInt32());

        var wage = items[2];
        Assert.Equal("INCOME", wage.GetProperty("nature").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, wage.GetProperty("classifier").ValueKind);
        Assert.Equal(5000m, wage.GetProperty("total").GetDecimal());
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

        var (_, json, _) = await GetAsync("?from=2026-10-01&to=2026-10-01");
        using var doc = json;

        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Aluguel", item.GetProperty("accountName").GetString());
        Assert.Equal(1800m, item.GetProperty("total").GetDecimal());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?to=2026-10-31")]
    [InlineData("?from=ontem&to=2026-10-31")]
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
