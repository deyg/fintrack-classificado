using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Accounts.ListAccounts;
using FinTrack.Domain.Accounts;

namespace FinTrack.Tests.Accounts;

public sealed class ListAccountsHandlerTests
{
    private sealed class FakeAccounts : IAccountRepository
    {
        public (int? Category, bool? Active, ExpenseClassifier? Classifier)? Received { get; private set; }

        public Task AddAsync(Account account, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<Account?>(null);

        public Task UpdateAsync(Account account, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Account>> ListAsync(
            int? categoryId, bool? active, ExpenseClassifier? classifier, CancellationToken cancellationToken)
        {
            Received = (categoryId, active, classifier);
            return Task.FromResult<IReadOnlyList<Account>>([]);
        }
    }

    [Fact]
    public async Task Repassa_filtros_traduzidos_ao_repositorio()
    {
        var repository = new FakeAccounts();

        var result = await new ListAccountsHandler(repository)
            .HandleAsync(new ListAccountsQuery(4, false, "NOF"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((4, false, ExpenseClassifier.NOF), repository.Received);
    }

    [Theory]
    [InlineData(0, null, "categoryId")]
    [InlineData(null, "zz", "classifier")]
    [InlineData(null, "", "classifier")]
    public async Task Filtros_invalidos_retornam_erro_sem_consultar(int? categoryId, string? classifier, string field)
    {
        var repository = new FakeAccounts();

        var result = await new ListAccountsHandler(repository)
            .HandleAsync(new ListAccountsQuery(categoryId, null, classifier), CancellationToken.None);

        Assert.Contains(field, result.ValidationErrors!.Keys);
        Assert.Null(repository.Received);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ListAccountsHandler(new FakeAccounts()).HandleAsync(new ListAccountsQuery(null, null, null), cts.Token));
    }
}
