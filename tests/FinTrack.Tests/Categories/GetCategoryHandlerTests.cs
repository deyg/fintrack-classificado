using System.Reflection;
using FinTrack.Application.Features.Categories;
using FinTrack.Application.Features.Categories.GetCategory;
using FinTrack.Domain.Categories;

namespace FinTrack.Tests.Categories;

public sealed class GetCategoryHandlerTests
{
    private sealed class FakeRepository(Category? stored) : ICategoryRepository
    {
        public int GetCalls { get; private set; }

        public int? LastId { get; private set; }

        public Task AddAsync(Category category, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Category>> ListAsync(CategoryNature? nature, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Category>>([]);

        public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            GetCalls++;
            LastId = id;
            return Task.FromResult(stored);
        }
    }

    private static Category Stored(int id)
    {
        var category = Category.Create("Mercado", CategoryNature.Expense);
        typeof(Category).GetProperty(nameof(Category.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(category, id);
        return category;
    }

    [Fact]
    public async Task Existente_retorna_categoria_com_natureza_externa()
    {
        var repository = new FakeRepository(Stored(7));

        var result = await new GetCategoryHandler(repository).HandleAsync(new GetCategoryQuery(7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new CategoryResult(7, "Mercado", "EXPENSE"), result.Value);
        Assert.Equal(7, repository.LastId);
    }

    [Fact]
    public async Task Inexistente_retorna_not_found()
    {
        var result = await new GetCategoryHandler(new FakeRepository(null)).HandleAsync(new GetCategoryQuery(999), CancellationToken.None);

        Assert.True(result.IsNotFound);
        Assert.False(result.IsSuccess);
        Assert.Null(result.ValidationErrors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task Id_menor_que_1_retorna_erro_de_id_sem_consultar(int id)
    {
        var repository = new FakeRepository(null);

        var result = await new GetCategoryHandler(repository).HandleAsync(new GetCategoryQuery(id), CancellationToken.None);

        Assert.False(result.IsNotFound);
        Assert.Contains("id", result.ValidationErrors!.Keys);
        Assert.Equal(0, repository.GetCalls);
    }

    [Fact]
    public async Task Fronteira_int_max_e_valida_e_consulta_o_repositorio()
    {
        var repository = new FakeRepository(null);

        var result = await new GetCategoryHandler(repository).HandleAsync(new GetCategoryQuery(int.MaxValue), CancellationToken.None);

        Assert.True(result.IsNotFound);
        Assert.Equal(int.MaxValue, repository.LastId);
    }

    [Fact]
    public async Task Token_cancelado_interrompe_antes_de_consultar()
    {
        var repository = new FakeRepository(null);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GetCategoryHandler(repository).HandleAsync(new GetCategoryQuery(1), cts.Token));
        Assert.Equal(0, repository.GetCalls);
    }
}
