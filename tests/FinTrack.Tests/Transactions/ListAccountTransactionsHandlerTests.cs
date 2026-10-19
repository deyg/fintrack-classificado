using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Transactions;
using FinTrack.Application.Features.Transactions.ListAccountTransactions;
using FinTrack.Application.Features.Transactions.ListTransactions;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;

namespace FinTrack.Tests.Transactions;

public sealed class ListAccountTransactionsHandlerTests
{
    private sealed class FakeAccounts(Account? stored) : IAccountRepository
    {
        public Task AddAsync(Account account, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(stored);

        public Task UpdateAsync(Account account, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<IReadOnlyList<Account>> ListAsync(
            int? categoryId, bool? active, ExpenseClassifier? classifier, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Account>>([]);
    }

    private sealed class FakeTransactions : ITransactionRepository
    {
        public TransactionFilter? Filter { get; private set; }

        public int ListCalls { get; private set; }

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<Transaction?>(null);

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<TransactionPageData> ListAsync(
            TransactionFilter filter, int page, int pageSize, CancellationToken cancellationToken)
        {
            ListCalls++;
            Filter = filter;
            return Task.FromResult(new TransactionPageData([], 0));
        }
    }

    private static ListAccountTransactionsHandler Build(Account? account, FakeTransactions repository) =>
        new(new FakeAccounts(account), new ListTransactionsHandler(repository));

    private static Account AnAccount() => Account.Create(1, "Conta", CategoryNature.Expense, ExpenseClassifier.OF);

    [Fact]
    public async Task Filtra_pela_conta_do_caminho()
    {
        var repository = new FakeTransactions();

        var result = await Build(AnAccount(), repository)
            .HandleAsync(new ListAccountTransactionsQuery(7, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, repository.Filter!.AccountId);
        Assert.Null(repository.Filter.CategoryId);
    }

    [Fact]
    public async Task Conta_inexistente_retorna_nao_encontrado_sem_listar()
    {
        var repository = new FakeTransactions();

        var result = await Build(null, repository)
            .HandleAsync(new ListAccountTransactionsQuery(7, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsNotFound);
        Assert.Equal(0, repository.ListCalls);
    }

    [Fact]
    public async Task Id_invalido_retorna_erro_de_id()
    {
        var result = await Build(AnAccount(), new FakeTransactions())
            .HandleAsync(new ListAccountTransactionsQuery(0, null, null, null, null), CancellationToken.None);

        Assert.Contains(ListAccountTransactionsHandler.IdField, result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Periodo_invertido_e_paginacao_invalida_sao_repassados()
    {
        var handler = Build(AnAccount(), new FakeTransactions());

        var period = await handler.HandleAsync(
            new ListAccountTransactionsQuery(1, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), null, null), CancellationToken.None);
        var paging = await handler.HandleAsync(
            new ListAccountTransactionsQuery(1, null, null, 0, 101), CancellationToken.None);

        Assert.Equal([ListTransactionsHandler.PeriodField], period.ValidationErrors!.Keys.ToArray());
        Assert.Equal(["page", "pageSize"], paging.ValidationErrors!.Keys.Order().ToArray());
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Build(AnAccount(), new FakeTransactions())
                .HandleAsync(new ListAccountTransactionsQuery(1, null, null, null, null), cts.Token));
    }
}
