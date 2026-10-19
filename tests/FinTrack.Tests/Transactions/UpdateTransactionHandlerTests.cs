using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Transactions;
using FinTrack.Application.Features.Transactions.UpdateTransaction;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;

namespace FinTrack.Tests.Transactions;

public sealed class UpdateTransactionHandlerTests
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

    private sealed class FakeTransactions(Transaction? stored) : ITransactionRepository
    {
        public int Updates { get; private set; }

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(stored);

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            Updates++;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<TransactionPageData> ListAsync(
            TransactionFilter filter, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new TransactionPageData([], 0));
    }

    private static Account Account_(bool active = true)
    {
        var account = Account.Create(1, "Mercado", CategoryNature.Expense, ExpenseClassifier.OF);
        if (!active)
        {
            account.Deactivate();
        }

        return account;
    }

    private static Transaction Stored() => Transaction.Create(5, 100m, new DateOnly(2026, 10, 1), "orig");

    private static UpdateTransactionHandler Build(Transaction? stored, Account? account, out FakeTransactions repository)
    {
        repository = new FakeTransactions(stored);
        return new(new UpdateTransactionValidator(), new FakeAccounts(account), repository,
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero)));
    }

    private static UpdateTransactionCommand Command(
        int id = 1, int? accountId = null, decimal? amount = null, DateOnly? date = null,
        bool descriptionProvided = false, string? description = null) =>
        new(id, accountId, amount, date, descriptionProvided, description);

    [Fact]
    public async Task Atualizacao_parcial_mantem_demais_campos()
    {
        var handler = Build(Stored(), null, out var repository);

        var result = await handler.HandleAsync(Command(amount: 250.75m), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(250.75m, result.Value!.Amount);
        Assert.Equal(5, result.Value.AccountId);
        Assert.Equal(new DateOnly(2026, 10, 1), result.Value.RealizedAt);
        Assert.Equal("orig", result.Value.Description);
        Assert.Equal(1, repository.Updates);
    }

    [Fact]
    public async Task Descricao_nula_explicita_limpa_e_ausente_mantem()
    {
        var cleared = await Build(Stored(), null, out _)
            .HandleAsync(Command(descriptionProvided: true, description: null), CancellationToken.None);
        var kept = await Build(Stored(), null, out _)
            .HandleAsync(Command(amount: 1m), CancellationToken.None);

        Assert.Null(cleared.Value!.Description);
        Assert.Equal("orig", kept.Value!.Description);
    }

    [Fact]
    public async Task Troca_para_conta_ativa_existente_e_aceita()
    {
        var result = await Build(Stored(), Account_(), out _)
            .HandleAsync(Command(accountId: 9), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(9, result.Value!.AccountId);
    }

    [Fact]
    public async Task Troca_para_conta_inexistente_retorna_422_em_accountId()
    {
        var result = await Build(Stored(), null, out var repository)
            .HandleAsync(Command(accountId: 9), CancellationToken.None);

        Assert.Contains("accountId", result.ValidationErrors!.Keys);
        Assert.Equal(0, repository.Updates);
    }

    [Fact]
    public async Task Troca_para_conta_inativa_e_conflito()
    {
        var result = await Build(Stored(), Account_(active: false), out var repository)
            .HandleAsync(Command(accountId: 9), CancellationToken.None);

        Assert.True(result.IsConflict);
        Assert.Equal(0, repository.Updates);
    }

    [Fact]
    public async Task Mesma_conta_nao_exige_conta_ativa_nem_existente()
    {
        var result = await Build(Stored(), null, out _)
            .HandleAsync(Command(accountId: 5, amount: 1m), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Data_de_hoje_e_aceita_e_amanha_e_rejeitada()
    {
        var today = await Build(Stored(), null, out _).HandleAsync(Command(date: Today), CancellationToken.None);
        var tomorrow = await Build(Stored(), null, out var repository)
            .HandleAsync(Command(date: Today.AddDays(1)), CancellationToken.None);

        Assert.True(today.IsSuccess);
        Assert.Contains("realizedAt", tomorrow.ValidationErrors!.Keys);
        Assert.Equal(0, repository.Updates);
    }

    [Fact]
    public async Task Corpo_sem_campos_e_rejeitado()
    {
        var result = await Build(Stored(), null, out _).HandleAsync(Command(), CancellationToken.None);

        Assert.Contains("body", result.ValidationErrors!.Keys);
    }

    [Theory]
    [InlineData(0, "amount")]
    [InlineData(-1, "amount")]
    public async Task Valor_invalido_e_rejeitado(int amount, string field)
    {
        var result = await Build(Stored(), null, out _).HandleAsync(Command(amount: amount), CancellationToken.None);

        Assert.Contains(field, result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Valor_com_tres_casas_e_descricao_longa_sao_rejeitados()
    {
        var scale = await Build(Stored(), null, out _).HandleAsync(Command(amount: 1.005m), CancellationToken.None);
        var tooLong = await Build(Stored(), null, out _)
            .HandleAsync(Command(descriptionProvided: true, description: new string('a', 501)), CancellationToken.None);

        Assert.Contains("amount", scale.ValidationErrors!.Keys);
        Assert.Contains("description", tooLong.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Inexistente_retorna_nao_encontrado()
    {
        var result = await Build(null, null, out _).HandleAsync(Command(amount: 1m), CancellationToken.None);

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task Id_invalido_retorna_erro_de_id()
    {
        var result = await Build(Stored(), null, out _).HandleAsync(Command(id: 0, amount: 1m), CancellationToken.None);

        Assert.Contains(UpdateTransactionHandler.IdField, result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Build(Stored(), null, out _).HandleAsync(Command(amount: 1m), cts.Token));
    }
}
