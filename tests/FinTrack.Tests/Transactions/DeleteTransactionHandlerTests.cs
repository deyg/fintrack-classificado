using FinTrack.Application.Features.Transactions;
using FinTrack.Application.Features.Transactions.DeleteTransaction;
using FinTrack.Domain.Transactions;

namespace FinTrack.Tests.Transactions;

public sealed class DeleteTransactionHandlerTests
{
    private sealed class FakeTransactions(bool deletes) : ITransactionRepository
    {
        public int DeleteCalls { get; private set; }

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<Transaction?>(null);

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            DeleteCalls++;
            return Task.FromResult(deletes);
        }

        public Task<TransactionPageData> ListAsync(
            TransactionFilter filter, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new TransactionPageData([], 0));
    }

    [Fact]
    public async Task Existente_e_removido()
    {
        var result = await new DeleteTransactionHandler(new FakeTransactions(true))
            .HandleAsync(new DeleteTransactionCommand(1), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Inexistente_retorna_nao_encontrado()
    {
        var result = await new DeleteTransactionHandler(new FakeTransactions(false))
            .HandleAsync(new DeleteTransactionCommand(1), CancellationToken.None);

        Assert.True(result.IsNotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Id_invalido_e_rejeitado_sem_excluir(int id)
    {
        var repository = new FakeTransactions(true);

        var result = await new DeleteTransactionHandler(repository).HandleAsync(new DeleteTransactionCommand(id), CancellationToken.None);

        Assert.Contains("id", result.ValidationErrors!.Keys);
        Assert.Equal(0, repository.DeleteCalls);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DeleteTransactionHandler(new FakeTransactions(true)).HandleAsync(new DeleteTransactionCommand(1), cts.Token));
    }
}
