using FinTrack.Tests.Architecture;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Tests.Http;

// Fechamento de Relatórios: os quatro relatórios devem ser coerentes entre si e com a fonte transacional.
public sealed class ReportsClosureContractTests : ReportContractTestBase
{
    private const string Period = "?from=2026-10-01&to=2026-10-31";

    protected override string Path => "summary";

    private async Task<System.Text.Json.JsonElement> ReadAsync(string report, string query = Period)
    {
        var (status, json, _) = await GetReportAsync(report, query);
        Assert.Equal(System.Net.HttpStatusCode.OK, status);
        return json.RootElement.Clone();
    }

    [Fact]
    public async Task Quatro_relatorios_sao_coerentes_com_a_fonte_transacional()
    {
        await SeedAsync();

        var summary = await ReadAsync("summary");
        var income = summary.GetProperty("income").GetDecimal();
        var expenses = summary.GetProperty("expenses").GetDecimal();
        Assert.Equal(income - expenses, summary.GetProperty("balance").GetDecimal());
        Assert.Equal((5000m, 2130.50m), (income, expenses));

        var categories = (await ReadAsync("by-category")).GetProperty("items").EnumerateArray().ToArray();
        var accounts = (await ReadAsync("by-account")).GetProperty("items").EnumerateArray().ToArray();
        var classifiers = (await ReadAsync("by-classifier")).GetProperty("items").EnumerateArray().ToArray();

        decimal SumOf(System.Text.Json.JsonElement[] items, string nature) =>
            items.Where(i => i.GetProperty("nature").GetString() == nature).Sum(i => i.GetProperty("total").GetDecimal());

        Assert.Equal(income, SumOf(categories, "INCOME"));
        Assert.Equal(expenses, SumOf(categories, "EXPENSE"));
        Assert.Equal(income, SumOf(accounts, "INCOME"));
        Assert.Equal(expenses, SumOf(accounts, "EXPENSE"));
        Assert.Equal(expenses, classifiers.Sum(i => i.GetProperty("total").GetDecimal()));

        var transactionCount = 4;
        Assert.Equal(transactionCount, categories.Sum(i => i.GetProperty("transactionCount").GetInt32()));
        Assert.Equal(transactionCount, accounts.Sum(i => i.GetProperty("transactionCount").GetInt32()));
        Assert.Equal(transactionCount - 1, classifiers.Sum(i => i.GetProperty("transactionCount").GetInt32()));
    }

    [Fact]
    public async Task Consultas_repetidas_sao_idempotentes_e_nao_persistem_agregados()
    {
        await SeedAsync();
        var before = await CountTransactionsAsync();
        var first = await ReadAsync("summary");

        var second = await ReadAsync("summary");

        Assert.Equal(first.GetProperty("balance").GetDecimal(), second.GetProperty("balance").GetDecimal());
        Assert.Equal(before, await CountTransactionsAsync());
    }

    [Fact]
    public void Quatro_operacoes_de_relatorio_estao_no_contrato_e_registradas()
    {
        var yaml = File.ReadAllText(System.IO.Path.Combine(
            ProjectReferenceRules.FindRepositoryRoot(), "docs", "contracts", "fintrack-openapi.yaml"));
        var endpoints = Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(e => e.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName)
            .ToHashSet();

        foreach (var operation in new[] { "getFinancialSummary", "getReportByCategory", "getReportByAccount", "getReportByClassifier" })
        {
            Assert.Contains($"operationId: {operation}", yaml);
            Assert.Contains(operation, endpoints);
        }
    }
}
