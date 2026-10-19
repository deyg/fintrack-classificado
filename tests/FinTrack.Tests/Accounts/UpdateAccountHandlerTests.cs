using System.Reflection;
using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Accounts.UpdateAccount;
using FinTrack.Application.Features.Categories;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;

namespace FinTrack.Tests.Accounts;

public sealed class UpdateAccountHandlerTests
{
    private sealed class FakeCategories(params Category[] stored) : ICategoryRepository
    {
        public Task AddAsync(Category category, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Category>> ListAsync(CategoryNature? nature, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Category>>(stored);

        public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(stored.SingleOrDefault(c => c.Id == id));

        public Task UpdateAsync(Category category, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> HasAccountsAsync(int categoryId, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FakeAccounts(Account? stored) : IAccountRepository
    {
        public int Updates { get; private set; }

        public Task AddAsync(Account account, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(stored);

        public Task UpdateAsync(Account account, CancellationToken cancellationToken)
        {
            Updates++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Account>> ListAsync(
            int? categoryId, bool? active, ExpenseClassifier? classifier, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Account>>([]);
    }

    private sealed class FakeHistory(bool has) : IAccountHistoryChecker
    {
        public Task<bool> HasTransactionsAsync(int accountId, CancellationToken cancellationToken) => Task.FromResult(has);
    }

    private static Category Cat(int id, CategoryNature nature)
    {
        var category = Category.Create("Cat" + id, nature);
        typeof(Category).GetProperty(nameof(Category.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(category, id);
        return category;
    }

    private static readonly Category[] Categories =
        [Cat(1, CategoryNature.Expense), Cat(2, CategoryNature.Expense), Cat(3, CategoryNature.Income)];

    private static Account Stored() => Account.Create(1, "Aluguel", CategoryNature.Expense, ExpenseClassifier.OF);

    private static UpdateAccountHandler Handler(FakeAccounts accounts, bool hasTransactions = false) =>
        new(new UpdateAccountValidator(), new FakeCategories(Categories), accounts, new FakeHistory(hasTransactions));

    [Fact]
    public async Task Renomeia_sem_lancamentos()
    {
        var accounts = new FakeAccounts(Stored());

        var result = await Handler(accounts).HandleAsync(new UpdateAccountCommand(7, null, " Casa ", false, null), CancellationToken.None);

        Assert.Equal("Casa", result.Value!.Name);
        Assert.Equal(1, accounts.Updates);
    }

    [Fact]
    public async Task Muda_categoria_e_classificador_sem_lancamentos()
    {
        var accounts = new FakeAccounts(Stored());

        var result = await Handler(accounts).HandleAsync(new UpdateAccountCommand(7, 2, null, true, "NOV"), CancellationToken.None);

        Assert.Equal(2, result.Value!.CategoryId);
        Assert.Equal("NOV", result.Value.Classifier);
    }

    [Fact]
    public async Task Muda_para_categoria_de_receita_exige_classifier_nulo_explicito()
    {
        var withoutClassifier = await Handler(new FakeAccounts(Stored()))
            .HandleAsync(new UpdateAccountCommand(7, 3, null, false, null), CancellationToken.None);
        var cleared = await Handler(new FakeAccounts(Stored()))
            .HandleAsync(new UpdateAccountCommand(7, 3, null, true, null), CancellationToken.None);

        Assert.Contains("classifier", withoutClassifier.ValidationErrors!.Keys);
        Assert.True(cleared.IsSuccess);
        Assert.Null(cleared.Value!.Classifier);
        Assert.Equal(3, cleared.Value.CategoryId);
    }

    [Fact]
    public async Task Limpar_classificador_em_conta_de_despesa_e_invalido()
    {
        var result = await Handler(new FakeAccounts(Stored()))
            .HandleAsync(new UpdateAccountCommand(7, null, null, true, null), CancellationToken.None);

        Assert.Contains("classifier", result.ValidationErrors!.Keys);
    }

    [Theory]
    [InlineData(2, false, null)]
    [InlineData(null, true, "OV")]
    [InlineData(2, true, "OV")]
    public async Task Com_lancamentos_mudar_categoria_ou_classificador_gera_conflito_sem_gravar(int? categoryId, bool provided, string? classifier)
    {
        var accounts = new FakeAccounts(Stored());

        var result = await Handler(accounts, hasTransactions: true)
            .HandleAsync(new UpdateAccountCommand(7, categoryId, null, provided, classifier), CancellationToken.None);

        Assert.True(result.IsConflict);
        Assert.Equal(0, accounts.Updates);
    }

    [Fact]
    public async Task Com_lancamentos_o_nome_ainda_pode_mudar()
    {
        var accounts = new FakeAccounts(Stored());

        var result = await Handler(accounts, hasTransactions: true)
            .HandleAsync(new UpdateAccountCommand(7, null, "Novo", false, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, accounts.Updates);
    }

    [Fact]
    public async Task Valores_iguais_aos_atuais_com_lancamentos_sao_no_op()
    {
        var accounts = new FakeAccounts(Stored());

        var result = await Handler(accounts, hasTransactions: true)
            .HandleAsync(new UpdateAccountCommand(7, 1, "Aluguel", true, "OF"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, accounts.Updates);
    }

    [Fact]
    public async Task Categoria_destino_inexistente_retorna_422_de_categoryId()
    {
        var result = await Handler(new FakeAccounts(Stored()))
            .HandleAsync(new UpdateAccountCommand(7, 99, null, false, null), CancellationToken.None);

        Assert.Contains("categoryId", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Conta_inexistente_retorna_not_found()
    {
        var result = await Handler(new FakeAccounts(null))
            .HandleAsync(new UpdateAccountCommand(7, null, "X", false, null), CancellationToken.None);

        Assert.True(result.IsNotFound);
    }

    [Theory]
    [InlineData(null, null, false, "body")]
    [InlineData(null, "", false, "name")]
    [InlineData(null, "   ", false, "name")]
    [InlineData(0, null, false, "categoryId")]
    [InlineData(null, null, true, "classifier")]
    public async Task Entradas_invalidas_retornam_erro_por_campo(int? categoryId, string? name, bool provided, string field)
    {
        // classifier "ZZ" no último caso: formato inválido, não só incoerência
        var classifier = provided ? "ZZ" : null;

        var result = await Handler(new FakeAccounts(Stored()))
            .HandleAsync(new UpdateAccountCommand(7, categoryId, name, provided, classifier), CancellationToken.None);

        Assert.Contains(field, result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Id_menor_que_1_retorna_erro_de_id()
    {
        var result = await Handler(new FakeAccounts(Stored()))
            .HandleAsync(new UpdateAccountCommand(0, null, "X", false, null), CancellationToken.None);

        Assert.Contains(UpdateAccountHandler.IdField, result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Handler(new FakeAccounts(Stored())).HandleAsync(new UpdateAccountCommand(7, null, "X", false, null), cts.Token));
    }
}
