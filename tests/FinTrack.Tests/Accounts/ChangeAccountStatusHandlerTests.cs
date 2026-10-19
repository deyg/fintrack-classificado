using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Accounts.ChangeAccountStatus;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;

namespace FinTrack.Tests.Accounts;

public sealed class ChangeAccountStatusHandlerTests
{
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

    private static Account Stored() => Account.Create(1, "Aluguel", CategoryNature.Expense, ExpenseClassifier.OF);

    [Fact]
    public async Task Desativa_e_reativa_gravando_cada_mudanca()
    {
        var repository = new FakeAccounts(Stored());
        var handler = new ChangeAccountStatusHandler(repository);

        var off = await handler.HandleAsync(new ChangeAccountStatusCommand(1, false), CancellationToken.None);
        var on = await handler.HandleAsync(new ChangeAccountStatusCommand(1, true), CancellationToken.None);

        Assert.False(off.Value!.Active);
        Assert.True(on.Value!.Active);
        Assert.Equal(2, repository.Updates);
    }

    [Fact]
    public async Task Repetir_o_estado_atual_e_idempotente_e_nao_grava()
    {
        var repository = new FakeAccounts(Stored());
        var handler = new ChangeAccountStatusHandler(repository);

        var result = await handler.HandleAsync(new ChangeAccountStatusCommand(1, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Active);
        Assert.Equal(0, repository.Updates);
    }

    [Fact]
    public async Task Inexistente_retorna_not_found()
    {
        var result = await new ChangeAccountStatusHandler(new FakeAccounts(null))
            .HandleAsync(new ChangeAccountStatusCommand(1, true), CancellationToken.None);

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task Active_ausente_retorna_erro_de_campo()
    {
        var result = await new ChangeAccountStatusHandler(new FakeAccounts(Stored()))
            .HandleAsync(new ChangeAccountStatusCommand(1, null), CancellationToken.None);

        Assert.Contains("active", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Id_invalido_retorna_erro_de_id()
    {
        var result = await new ChangeAccountStatusHandler(new FakeAccounts(Stored()))
            .HandleAsync(new ChangeAccountStatusCommand(0, true), CancellationToken.None);

        Assert.Contains(ChangeAccountStatusHandler.IdField, result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ChangeAccountStatusHandler(new FakeAccounts(Stored()))
                .HandleAsync(new ChangeAccountStatusCommand(1, true), cts.Token));
    }
}
