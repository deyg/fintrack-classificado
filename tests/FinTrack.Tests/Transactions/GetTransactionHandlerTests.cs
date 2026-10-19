using FinTrack.Application.Features.Transactions;
using FinTrack.Application.Features.Transactions.GetTransaction;
using FinTrack.Domain.Transactions;

namespace FinTrack.Tests.Transactions;

public sealed class GetTransactionHandlerTests
{
    private sealed class FakeTransactions(Transaction? stored) : ITransactionRepository
    {
        public int Calls { get; private set; }

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(stored);
        }

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<TransactionPageData> ListAsync(
            TransactionFilter filter, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new TransactionPageData([], 0));
    }

    [Fact]
    public async Task Existente_retorna_sucesso_com_campos()
    {
        var transaction = Transaction.Create(7, 10.5m, new DateOnly(2026, 1, 2), "x");

        var result = await new GetTransactionHandler(new FakeTransactions(transaction))
            .HandleAsync(new GetTransactionQuery(1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value!.AccountId);
        Assert.Equal(10.5m, result.Value.Amount);
        Assert.Equal("x", result.Value.Description);
    }

    [Fact]
    public async Task Inexistente_retorna_nao_encontrado()
    {
        var result = await new GetTransactionHandler(new FakeTransactions(null))
            .HandleAsync(new GetTransactionQuery(1), CancellationToken.None);

        Assert.True(result.IsNotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Id_invalido_e_rejeitado_sem_consultar(int id)
    {
        var repository = new FakeTransactions(null);

        var result = await new GetTransactionHandler(repository).HandleAsync(new GetTransactionQuery(id), CancellationToken.None);

        Assert.Contains("id", result.ValidationErrors!.Keys);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GetTransactionHandler(new FakeTransactions(null)).HandleAsync(new GetTransactionQuery(1), cts.Token));
    }
}
