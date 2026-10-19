using FinTrack.Application.Features.Categories;
using FinTrack.Application.Features.Categories.DeleteCategory;
using FinTrack.Domain.Categories;

namespace FinTrack.Tests.Categories;

public sealed class DeleteCategoryHandlerTests
{
    private sealed class FakeRepository(Category? stored, bool hasAccounts = false) : ICategoryRepository
    {
        public int Deletes { get; private set; }

        public Task AddAsync(Category category, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Category>> ListAsync(CategoryNature? nature, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Category>>([]);

        public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(stored);

        public Task UpdateAsync(Category category, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> HasAccountsAsync(int categoryId, CancellationToken cancellationToken) => Task.FromResult(hasAccounts);

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            Deletes++;
            return Task.FromResult(true);
        }
    }

    private static Category Stored() => Category.Create("Mercado", CategoryNature.Expense);

    [Fact]
    public async Task Sem_contas_exclui()
    {
        var repository = new FakeRepository(Stored());

        var result = await new DeleteCategoryHandler(repository).HandleAsync(new DeleteCategoryCommand(5), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.Deletes);
    }

    [Fact]
    public async Task Com_contas_retorna_conflito_sem_excluir()
    {
        var repository = new FakeRepository(Stored(), hasAccounts: true);

        var result = await new DeleteCategoryHandler(repository).HandleAsync(new DeleteCategoryCommand(5), CancellationToken.None);

        Assert.True(result.IsConflict);
        Assert.Equal(0, repository.Deletes);
    }

    [Fact]
    public async Task Inexistente_retorna_not_found()
    {
        var repository = new FakeRepository(null);

        var result = await new DeleteCategoryHandler(repository).HandleAsync(new DeleteCategoryCommand(5), CancellationToken.None);

        Assert.True(result.IsNotFound);
        Assert.Equal(0, repository.Deletes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Id_invalido_retorna_erro_de_id(int id)
    {
        var result = await new DeleteCategoryHandler(new FakeRepository(Stored())).HandleAsync(new DeleteCategoryCommand(id), CancellationToken.None);

        Assert.Contains("id", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DeleteCategoryHandler(new FakeRepository(Stored())).HandleAsync(new DeleteCategoryCommand(5), cts.Token));
    }
}
