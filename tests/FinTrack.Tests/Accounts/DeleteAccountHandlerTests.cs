using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Accounts.DeleteAccount;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;

namespace FinTrack.Tests.Accounts;

public sealed class DeleteAccountHandlerTests
{
    private sealed class FakeAccounts(Account? stored) : IAccountRepository
    {
        public int Deletes { get; private set; }

        public Task AddAsync(Account account, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(stored);

        public Task UpdateAsync(Account account, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            Deletes++;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<Account>> ListAsync(
            int? categoryId, bool? active, ExpenseClassifier? classifier, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Account>>([]);
    }

    private sealed class History(bool has) : IAccountHistoryChecker
    {
        public Task<bool> HasTransactionsAsync(int accountId, CancellationToken cancellationToken) => Task.FromResult(has);
    }

    private static Account Stored() => Account.Create(1, "Aluguel", CategoryNature.Expense, ExpenseClassifier.OF);

    [Fact]
    public async Task Sem_lancamentos_exclui()
    {
        var repository = new FakeAccounts(Stored());

        var result = await new DeleteAccountHandler(repository, new History(false))
            .HandleAsync(new DeleteAccountCommand(1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.Deletes);
    }

    [Fact]
    public async Task Com_lancamentos_retorna_conflito_sem_excluir()
    {
        var repository = new FakeAccounts(Stored());

        var result = await new DeleteAccountHandler(repository, new History(true))
            .HandleAsync(new DeleteAccountCommand(1), CancellationToken.None);

        Assert.True(result.IsConflict);
        Assert.Equal(0, repository.Deletes);
    }

    [Fact]
    public async Task Inexistente_retorna_not_found()
    {
        var result = await new DeleteAccountHandler(new FakeAccounts(null), new History(false))
            .HandleAsync(new DeleteAccountCommand(1), CancellationToken.None);

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task Id_invalido_retorna_erro_de_id()
    {
        var result = await new DeleteAccountHandler(new FakeAccounts(Stored()), new History(false))
            .HandleAsync(new DeleteAccountCommand(0), CancellationToken.None);

        Assert.Contains(DeleteAccountHandler.IdField, result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DeleteAccountHandler(new FakeAccounts(Stored()), new History(false))
                .HandleAsync(new DeleteAccountCommand(1), cts.Token));
    }
}
