using FinTrack.Application.Features.Reports;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;

namespace FinTrack.Tests.Reports;

public sealed class ReportDefinitionsTests
{
    private static readonly ReportPeriod Period = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

    [Fact]
    public void Periodo_valido_e_inclusivo_de_um_dia()
    {
        var day = new DateOnly(2026, 10, 5);

        Assert.True(ReportPeriodValidator.TryCreate(new ReportPeriodQuery(day, day), out var period, out var errors));
        Assert.Empty(errors);
        Assert.Equal(new ReportPeriod(day, day), period);
    }

    [Theory]
    [InlineData(false, true, "from")]
    [InlineData(true, false, "to")]
    public void Periodo_incompleto_e_erro_de_parametro_400(bool hasFrom, bool hasTo, string missing)
    {
        var query = new ReportPeriodQuery(hasFrom ? new DateOnly(2026, 1, 1) : null, hasTo ? new DateOnly(2026, 1, 31) : null);

        Assert.False(ReportPeriodValidator.TryCreate(query, out var period, out var errors));
        Assert.Null(period);
        Assert.Contains(missing, errors.Keys);
        Assert.False(ReportPeriodValidator.IsValueError(errors));
    }

    [Fact]
    public void Periodo_ausente_reporta_from_e_to()
    {
        ReportPeriodValidator.TryCreate(new ReportPeriodQuery(null, null), out _, out var errors);

        Assert.Equal(["from", "to"], errors.Keys.Order().ToArray());
    }

    [Fact]
    public void Periodo_invertido_e_erro_de_valor_422()
    {
        var query = new ReportPeriodQuery(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 31));

        Assert.False(ReportPeriodValidator.TryCreate(query, out _, out var errors));
        Assert.True(ReportPeriodValidator.IsValueError(errors));
    }

    [Fact]
    public void Resumo_calcula_resultado()
    {
        var result = SummaryReportResult.From(Period, new SummaryTotals(6200m, 2669.90m));

        Assert.Equal(3530.10m, result.Balance);
        Assert.Equal(new ReportPeriodResult(Period.From, Period.To), result.Period);
    }

    [Fact]
    public void Resumo_sem_dados_e_zerado_e_despesa_maior_gera_resultado_negativo()
    {
        var empty = SummaryReportResult.From(Period, new SummaryTotals(0, 0));

        Assert.Equal((0m, 0m, 0m), (empty.Income, empty.Expenses, empty.Balance));
        Assert.Equal(-50m, SummaryReportResult.From(Period, new SummaryTotals(100m, 150m)).Balance);
    }

    [Fact]
    public void Por_categoria_ordena_por_nome_sem_caixa_e_desempata_por_id()
    {
        var result = ByCategoryReportResult.From(Period,
        [
            new CategoryTotalRow(3, "mercado", CategoryNature.Expense, 10m, 1),
            new CategoryTotalRow(1, "Salario", CategoryNature.Income, 5m, 2),
            new CategoryTotalRow(2, "Mercado", CategoryNature.Expense, 20.5m, 3),
        ]);

        Assert.Equal([2, 3, 1], result.Items.Select(i => i.CategoryId));
        Assert.Equal("INCOME", result.Items[2].Nature);
        Assert.Equal(20.5m, result.Items[0].Total);
    }

    [Fact]
    public void Por_conta_ordena_por_nome_e_traduz_classificador_e_natureza()
    {
        var result = ByAccountReportResult.From(Period,
        [
            new AccountTotalRow(2, "Salario", 9, CategoryNature.Income, null, 10m, 1),
            new AccountTotalRow(1, "Aluguel", 8, CategoryNature.Expense, ExpenseClassifier.NOF, 20m, 2),
        ]);

        Assert.Equal(["Aluguel", "Salario"], result.Items.Select(i => i.AccountName));
        Assert.Equal("NOF", result.Items[0].Classifier);
        Assert.Null(result.Items[1].Classifier);
        Assert.Equal("INCOME", result.Items[1].Nature);
    }

    [Fact]
    public void Por_classificador_sempre_retorna_os_quatro_grupos_na_ordem()
    {
        var result = ByClassifierReportResult.From(Period,
        [
            new ClassifierTotalRow(ExpenseClassifier.NOV, 7m, 2),
            new ClassifierTotalRow(ExpenseClassifier.OF, 3m, 1),
        ]);

        Assert.Equal(["OF", "OV", "NOF", "NOV"], result.Items.Select(i => i.Classifier));
        Assert.Equal([3m, 0m, 0m, 7m], result.Items.Select(i => i.Total));
        Assert.Equal([1, 0, 0, 2], result.Items.Select(i => i.TransactionCount));
    }

    [Fact]
    public void Por_classificador_sem_dados_retorna_quatro_grupos_zerados()
    {
        var result = ByClassifierReportResult.From(Period, []);

        Assert.Equal(4, result.Items.Count);
        Assert.All(result.Items, i => Assert.Equal((0m, 0), (i.Total, i.TransactionCount)));
    }
}
