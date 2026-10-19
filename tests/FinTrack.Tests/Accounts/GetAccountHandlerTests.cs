using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Accounts.GetAccount;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;

namespace FinTrack.Tests.Accounts;

public sealed class GetAccountHandlerTests
{
    private sealed class FakeAccounts(Account? stored) : IAccountRepository
    {
        public int Lookups { get; private set; }

        public Task AddAsync(Account account, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult(stored);
        }

        public Task UpdateAsync(Account account, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Account>> ListAsync(
            int? categoryId, bool? active, ExpenseClassifier? classifier, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Account>>([]);
    }

    [Fact]
    public async Task Existente_retorna_conta()
    {
        var account = Account.Create(3, "Aluguel", CategoryNature.Expense, ExpenseClassifier.OF);

        var result = await new GetAccountHandler(new FakeAccounts(account)).HandleAsync(new GetAccountQuery(1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new AccountResult(0, 3, "Aluguel", "OF", true), result.Value);
    }

    [Fact]
    public async Task Inexistente_retorna_not_found()
    {
        var result = await new GetAccountHandler(new FakeAccounts(null)).HandleAsync(new GetAccountQuery(1), CancellationToken.None);

        Assert.True(result.IsNotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Id_invalido_retorna_erro_sem_consultar(int id)
    {
        var repository = new FakeAccounts(null);

        var result = await new GetAccountHandler(repository).HandleAsync(new GetAccountQuery(id), CancellationToken.None);

        Assert.Contains("id", result.ValidationErrors!.Keys);
        Assert.Equal(0, repository.Lookups);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GetAccountHandler(new FakeAccounts(null)).HandleAsync(new GetAccountQuery(1), cts.Token));
    }
}
