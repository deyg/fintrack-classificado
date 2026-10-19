using System.Globalization;
using FinTrack.Domain.Common;
using FinTrack.Domain.Transactions;

namespace FinTrack.Tests.Transactions;

public sealed class TransactionTests
{
    private static readonly DateOnly Date = new(2026, 10, 6);

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    [Fact]
    public void Create_valido_normaliza_descricao()
    {
        var transaction = Transaction.Create(12, 1800.50m, Date, "  Aluguel ");

        Assert.Equal(12, transaction.AccountId);
        Assert.Equal(1800.50m, transaction.Amount);
        Assert.Equal(Date, transaction.RealizedAt);
        Assert.Equal("Aluguel", transaction.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Descricao_vazia_vira_nula(string? description) =>
        Assert.Null(Transaction.Create(1, 1m, Date, description).Description);

    [Fact]
    public void Descricao_aceita_500_e_rejeita_501()
    {
        Assert.Equal(500, Transaction.Create(1, 1m, Date, new string('a', 500)).Description!.Length);

        var ex = Assert.Throws<DomainException>(() => Transaction.Create(1, 1m, Date, new string('a', 501)));

        Assert.Equal("transaction.description.too_long", ex.Code);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("-10")]
    public void Valor_zero_ou_negativo_e_rejeitado(string amount)
    {
        var ex = Assert.Throws<DomainException>(() => Transaction.Create(1, Parse(amount), Date, null));

        Assert.Equal("transaction.amount.not_positive", ex.Code);
    }

    [Theory]
    [InlineData("0.001")]
    [InlineData("10.999")]
    public void Valor_com_tres_casas_e_rejeitado(string amount)
    {
        var ex = Assert.Throws<DomainException>(() => Transaction.Create(1, Parse(amount), Date, null));

        Assert.Equal("transaction.amount.scale", ex.Code);
    }

    [Fact]
    public void Valor_minimo_e_duas_casas_sao_aceitos()
    {
        Assert.Equal(0.01m, Transaction.Create(1, 0.01m, Date, null).Amount);
        Assert.Equal(10m, Transaction.Create(1, 10.00m, Date, null).Amount);
    }

    [Fact]
    public void Data_padrao_e_conta_invalida_sao_rejeitadas()
    {
        Assert.Equal("transaction.realized_at.required",
            Assert.Throws<DomainException>(() => Transaction.Create(1, 1m, default, null)).Code);
        Assert.Equal("transaction.account.required",
            Assert.Throws<DomainException>(() => Transaction.Create(0, 1m, Date, null)).Code);
    }

    [Fact]
    public void Change_valida_antes_de_alterar_o_estado()
    {
        var transaction = Transaction.Create(1, 10m, Date, "A");

        Assert.Throws<DomainException>(() => transaction.Change(2, 0m, Date.AddDays(1), "B"));

        Assert.Equal(1, transaction.AccountId);
        Assert.Equal(10m, transaction.Amount);
        Assert.Equal(Date, transaction.RealizedAt);
        Assert.Equal("A", transaction.Description);

        transaction.Change(2, 20m, Date.AddDays(1), null);

        Assert.Equal(2, transaction.AccountId);
        Assert.Equal(20m, transaction.Amount);
        Assert.Null(transaction.Description);
    }
}
