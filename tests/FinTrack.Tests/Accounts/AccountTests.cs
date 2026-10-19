using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Common;

namespace FinTrack.Tests.Accounts;

public sealed class AccountTests
{
    [Theory]
    [InlineData(ExpenseClassifier.OF)]
    [InlineData(ExpenseClassifier.OV)]
    [InlineData(ExpenseClassifier.NOF)]
    [InlineData(ExpenseClassifier.NOV)]
    public void Conta_de_despesa_aceita_os_quatro_classificadores(ExpenseClassifier classifier)
    {
        var account = Account.Create(3, "Aluguel", CategoryNature.Expense, classifier);

        Assert.Equal(classifier, account.Classifier);
        Assert.Equal(3, account.CategoryId);
        Assert.Equal("Aluguel", account.Name);
        Assert.True(account.Active);
    }

    [Fact]
    public void Conta_de_receita_sem_classificador_e_valida()
    {
        var account = Account.Create(1, "Salário", CategoryNature.Income, null);

        Assert.Null(account.Classifier);
    }

    [Fact]
    public void Conta_de_despesa_sem_classificador_e_rejeitada()
    {
        var ex = Assert.Throws<DomainException>(() => Account.Create(1, "Aluguel", CategoryNature.Expense, null));

        Assert.Equal("account.classifier.required", ex.Code);
    }

    [Theory]
    [InlineData(ExpenseClassifier.OF)]
    [InlineData(ExpenseClassifier.NOV)]
    public void Conta_de_receita_com_classificador_e_rejeitada(ExpenseClassifier classifier)
    {
        var ex = Assert.Throws<DomainException>(() => Account.Create(1, "Salário", CategoryNature.Income, classifier));

        Assert.Equal("account.classifier.not_allowed", ex.Code);
    }

    [Fact]
    public void Classificador_fora_do_enum_e_rejeitado()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Account.Create(1, "Aluguel", CategoryNature.Expense, (ExpenseClassifier)99));

        Assert.Equal("account.classifier.invalid", ex.Code);
    }

    [Fact]
    public void Natureza_fora_do_enum_e_rejeitada()
    {
        var ex = Assert.Throws<DomainException>(() => Account.Create(1, "X", (CategoryNature)0, null));

        Assert.Equal("category.nature.invalid", ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Categoria_invalida_e_rejeitada(int categoryId)
    {
        var ex = Assert.Throws<DomainException>(() => Account.Create(categoryId, "X", CategoryNature.Income, null));

        Assert.Equal("account.category.required", ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_ausente_e_rejeitado(string? name)
    {
        var ex = Assert.Throws<DomainException>(() => Account.Create(1, name, CategoryNature.Income, null));

        Assert.Equal("account.name.required", ex.Code);
    }

    [Fact]
    public void Nome_e_aparado_e_limitado_a_100_caracteres()
    {
        Assert.Equal("Aluguel", Account.Create(1, "  Aluguel  ", CategoryNature.Income, null).Name);
        Assert.Equal(100, Account.Create(1, new string('a', 100), CategoryNature.Income, null).Name.Length);

        var ex = Assert.Throws<DomainException>(() =>
            Account.Create(1, new string('a', 101), CategoryNature.Income, null));
        Assert.Equal("account.name.too_long", ex.Code);
    }

    [Fact]
    public void Conta_desativada_preserva_dados_e_pode_ser_reativada()
    {
        var account = Account.Create(2, "Aluguel", CategoryNature.Expense, ExpenseClassifier.OF);

        account.Deactivate();
        Assert.False(account.Active);
        Assert.Equal("Aluguel", account.Name);
        Assert.Equal(ExpenseClassifier.OF, account.Classifier);
        Assert.Equal(2, account.CategoryId);

        account.Activate();
        Assert.True(account.Active);
    }

    [Fact]
    public void Ativar_e_desativar_sao_idempotentes()
    {
        var account = Account.Create(1, "Salário", CategoryNature.Income, null);

        account.Activate();
        Assert.True(account.Active);

        account.Deactivate();
        account.Deactivate();
        Assert.False(account.Active);
    }

    [Fact]
    public void Classificador_tem_os_quatro_valores_do_contrato()
    {
        Assert.Equal(["OF", "OV", "NOF", "NOV"], Enum.GetNames<ExpenseClassifier>());
    }
}
