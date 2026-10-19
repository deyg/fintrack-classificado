using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Transactions;
using FinTrack.Application.Features.Transactions.CreateTransaction;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;

namespace FinTrack.Tests.Transactions;

public sealed class CreateTransactionHandlerTests
{
    private static readonly DateOnly Today = new(2026, 10, 6);

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
        public List<Transaction> Added { get; } = [];

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            Added.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<Transaction?>(null);

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private static Account ActiveAccount() => Account.Create(1, "Aluguel", CategoryNature.Expense, ExpenseClassifier.OF);

    private static Account InactiveAccount()
    {
        var account = ActiveAccount();
        account.Deactivate();
        return account;
    }

    private static CreateTransactionHandler Build(Account? account, FakeTransactions transactions) =>
        new(new CreateTransactionValidator(), new FakeAccounts(account), transactions,
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero)));

    private static CreateTransactionCommand Command(
        int? accountId = 1, decimal? amount = 10m, DateOnly? date = null, string? description = null) =>
        new(accountId, amount, date ?? Today, description);

    [Fact]
    public async Task Cria_lancamento_valido_com_data_de_hoje()
    {
        var transactions = new FakeTransactions();

        var result = await Build(ActiveAccount(), transactions)
            .HandleAsync(Command(amount: 1800.50m, description: " Aluguel "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1800.50m, result.Value!.Amount);
        Assert.Equal("Aluguel", result.Value.Description);
        Assert.Equal(Today, result.Value.RealizedAt);
        Assert.Single(transactions.Added);
    }

    [Fact]
    public async Task Data_de_ontem_e_aceita_e_amanha_e_rejeitada()
    {
        var transactions = new FakeTransactions();
        var handler = Build(ActiveAccount(), transactions);

        var yesterday = await handler.HandleAsync(Command(date: Today.AddDays(-1)), CancellationToken.None);
        var tomorrow = await handler.HandleAsync(Command(date: Today.AddDays(1)), CancellationToken.None);

        Assert.True(yesterday.IsSuccess);
        Assert.Contains("realizedAt", tomorrow.ValidationErrors!.Keys);
        Assert.Single(transactions.Added);
    }

    [Fact]
    public async Task Conta_inativa_retorna_conflito_sem_gravar()
    {
        var transactions = new FakeTransactions();

        var result = await Build(InactiveAccount(), transactions).HandleAsync(Command(), CancellationToken.None);

        Assert.True(result.IsConflict);
        Assert.Empty(transactions.Added);
    }

    [Fact]
    public async Task Conta_inexistente_retorna_erro_em_accountId()
    {
        var result = await Build(null, new FakeTransactions()).HandleAsync(Command(), CancellationToken.None);

        Assert.Contains("accountId", result.ValidationErrors!.Keys);
    }

    [Theory]
    [InlineData(null, "amount")]
    [InlineData(0, "amount")]
    [InlineData(-5, "amount")]
    public async Task Valor_ausente_zero_ou_negativo_e_rejeitado(int? amount, string field)
    {
        var result = await Build(ActiveAccount(), new FakeTransactions())
            .HandleAsync(Command(amount: amount), CancellationToken.None);

        Assert.Contains(field, result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Tres_casas_e_valor_acima_do_limite_sao_rejeitados()
    {
        var handler = Build(ActiveAccount(), new FakeTransactions());

        var scale = await handler.HandleAsync(Command(amount: 10.999m), CancellationToken.None);
        var tooBig = await handler.HandleAsync(Command(amount: 10_000_000_000_000_000m), CancellationToken.None);
        var max = await handler.HandleAsync(Command(amount: CreateTransactionValidator.MaxAmount), CancellationToken.None);

        Assert.Contains("amount", scale.ValidationErrors!.Keys);
        Assert.Contains("amount", tooBig.ValidationErrors!.Keys);
        Assert.True(max.IsSuccess);
    }

    [Fact]
    public async Task Campos_obrigatorios_e_descricao_longa_sao_rejeitados()
    {
        var handler = Build(ActiveAccount(), new FakeTransactions());

        var noAccount = await handler.HandleAsync(Command(accountId: null), CancellationToken.None);
        var noDate = await handler.HandleAsync(new CreateTransactionCommand(1, 10m, null, null), CancellationToken.None);
        var longText = await handler.HandleAsync(Command(description: new string('a', 501)), CancellationToken.None);

        Assert.Contains("accountId", noAccount.ValidationErrors!.Keys);
        Assert.Contains("realizedAt", noDate.ValidationErrors!.Keys);
        Assert.Contains("description", longText.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Build(ActiveAccount(), new FakeTransactions()).HandleAsync(Command(), cts.Token));
    }
}
