using System.Net;

namespace FinTrack.Tests.Http;

public sealed class ReportByClassifierContractTests : ReportContractTestBase
{
    protected override string Path => "by-classifier";

    [Fact]
    public void Contrato_openapi_declara_getReportByClassifier_com_200_400_422() =>
        AssertOpenApiDeclares("getReportByClassifier", string.Empty);

    [Fact]
    public async Task Retorna_sempre_os_quatro_grupos_somente_com_despesas()
    {
        await SeedAsync();

        var (status, json, _) = await GetAsync("?from=2026-10-01&to=2026-10-31");
        using var doc = json;

        Assert.Equal(HttpStatusCode.OK, status);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(["OF", "OV", "NOF", "NOV"], items.Select(i => i.GetProperty("classifier").GetString()));
        Assert.Equal([1800m, 0m, 0m, 330.50m], items.Select(i => i.GetProperty("total").GetDecimal()));
        Assert.Equal([1, 0, 0, 2], items.Select(i => i.GetProperty("transactionCount").GetInt32()));
    }

    [Fact]
    public async Task Receitas_nao_entram_em_nenhum_grupo()
    {
        await SeedAsync();

        var (_, json, _) = await GetAsync("?from=2026-10-05&to=2026-10-05");
        using var doc = json;

        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(4, items.Length);
        Assert.All(items, i => Assert.Equal(0m, i.GetProperty("total").GetDecimal()));
    }

    [Fact]
    public async Task Periodo_sem_lancamentos_retorna_quatro_grupos_zerados()
    {
        await SeedAsync();

        var (status, json, _) = await GetAsync("?from=2025-01-01&to=2025-01-31");
        using var doc = json;

        Assert.Equal(HttpStatusCode.OK, status);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(["OF", "OV", "NOF", "NOV"], items.Select(i => i.GetProperty("classifier").GetString()));
        Assert.All(items, i =>
        {
            Assert.Equal(0m, i.GetProperty("total").GetDecimal());
            Assert.Equal(0, i.GetProperty("transactionCount").GetInt32());
        });
    }

    [Fact]
    public async Task Periodo_de_um_dia_respeita_limites_inclusivos()
    {
        await SeedAsync();

        var (_, json, _) = await GetAsync("?from=2026-10-31&to=2026-10-31");
        using var doc = json;

        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal([0m, 0m, 0m, 80m], items.Select(i => i.GetProperty("total").GetDecimal()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-10-01")]
    [InlineData("?from=2026-10-01&to=xx")]
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
