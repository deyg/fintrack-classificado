using System.Reflection;
using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Accounts.CreateAccount;
using FinTrack.Application.Features.Categories;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;

namespace FinTrack.Tests.Accounts;

public sealed class CreateAccountHandlerTests
{
    private sealed class FakeCategories(Category? stored) : ICategoryRepository
    {
        public Task AddAsync(Category category, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Category>> ListAsync(CategoryNature? nature, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Category>>([]);

        public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(stored);

        public Task UpdateAsync(Category category, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> HasAccountsAsync(int categoryId, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FakeAccounts : IAccountRepository
    {
        public List<Account> Added { get; } = [];

        public Task AddAsync(Account account, CancellationToken cancellationToken)
        {
            typeof(Account).GetProperty(nameof(Account.Id), BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(account, 12);
            Added.Add(account);
            return Task.CompletedTask;
        }

        public Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<Account?>(null);

        public Task<IReadOnlyList<Account>> ListAsync(
            int? categoryId, bool? active, ExpenseClassifier? classifier, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Account>>([]);
    }

    private static Category StoredCategory(CategoryNature nature)
    {
        var category = Category.Create("Cat", nature);
        typeof(Category).GetProperty(nameof(Category.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(category, 3);
        return category;
    }

    private static CreateAccountHandler Handler(Category? category, FakeAccounts accounts) =>
        new(new CreateAccountValidator(), new FakeCategories(category), accounts);

    [Fact]
    public async Task Despesa_com_classificador_cria_conta_ativa()
    {
        var accounts = new FakeAccounts();

        var result = await Handler(StoredCategory(CategoryNature.Expense), accounts)
            .HandleAsync(new CreateAccountCommand(3, "  Aluguel ", "OF"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new AccountResult(12, 3, "Aluguel", "OF", true), result.Value);
        Assert.Single(accounts.Added);
    }

    [Theory]
    [InlineData(null)]
    public async Task Receita_sem_classificador_cria_conta(string? classifier)
    {
        var result = await Handler(StoredCategory(CategoryNature.Income), new FakeAccounts())
            .HandleAsync(new CreateAccountCommand(3, "Salario", classifier), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Classifier);
    }

    [Theory]
    [InlineData(CategoryNature.Expense, null)]
    [InlineData(CategoryNature.Income, "OF")]
    public async Task Classificador_incoerente_com_a_natureza_retorna_erro_de_classifier(CategoryNature nature, string? classifier)
    {
        var accounts = new FakeAccounts();

        var result = await Handler(StoredCategory(nature), accounts)
            .HandleAsync(new CreateAccountCommand(3, "X", classifier), CancellationToken.None);

        Assert.Contains("classifier", result.ValidationErrors!.Keys);
        Assert.Empty(accounts.Added);
    }

    [Fact]
    public async Task Categoria_inexistente_retorna_erro_de_categoryId()
    {
        var accounts = new FakeAccounts();

        var result = await Handler(null, accounts).HandleAsync(new CreateAccountCommand(99, "X", "OF"), CancellationToken.None);

        Assert.Contains("categoryId", result.ValidationErrors!.Keys);
        Assert.Empty(accounts.Added);
    }

    [Theory]
    [InlineData(null, "X", "OF", "categoryId")]
    [InlineData(0, "X", "OF", "categoryId")]
    [InlineData(3, null, "OF", "name")]
    [InlineData(3, "  ", "OF", "name")]
    [InlineData(3, "X", "of", "classifier")]
    [InlineData(3, "X", "ZZ", "classifier")]
    public async Task Valores_invalidos_retornam_erro_por_campo(int? categoryId, string? name, string? classifier, string field)
    {
        var accounts = new FakeAccounts();

        var result = await Handler(StoredCategory(CategoryNature.Expense), accounts)
            .HandleAsync(new CreateAccountCommand(categoryId, name, classifier), CancellationToken.None);

        Assert.Contains(field, result.ValidationErrors!.Keys);
        Assert.Empty(accounts.Added);
    }

    [Fact]
    public async Task Nome_com_101_caracteres_e_rejeitado_e_com_100_aceito()
    {
        var handler = Handler(StoredCategory(CategoryNature.Expense), new FakeAccounts());

        var tooLong = await handler.HandleAsync(new CreateAccountCommand(3, new string('a', 101), "OV"), CancellationToken.None);
        var limit = await handler.HandleAsync(new CreateAccountCommand(3, new string('b', 100), "OV"), CancellationToken.None);

        Assert.Contains("name", tooLong.ValidationErrors!.Keys);
        Assert.True(limit.IsSuccess);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Handler(StoredCategory(CategoryNature.Expense), new FakeAccounts())
                .HandleAsync(new CreateAccountCommand(3, "X", "OF"), cts.Token));
    }
}
