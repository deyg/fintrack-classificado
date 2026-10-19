using System.Reflection;
using FinTrack.Application.Features.Categories;
using FinTrack.Application.Features.Categories.ListCategories;
using FinTrack.Domain.Categories;

namespace FinTrack.Tests.Categories;

public sealed class ListCategoriesHandlerTests
{
    private sealed class FakeRepository(params Category[] stored) : ICategoryRepository
    {
        public int ListCalls { get; private set; }

        public CategoryNature? LastFilter { get; private set; }

        public Task AddAsync(Category category, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Category>> ListAsync(CategoryNature? nature, CancellationToken cancellationToken)
        {
            ListCalls++;
            LastFilter = nature;
            return Task.FromResult<IReadOnlyList<Category>>(
                stored.Where(c => nature is null || c.Nature == nature).ToList());
        }
    }

    private static Category Stored(int id, string name, CategoryNature nature)
    {
        var category = Category.Create(name, nature);
        typeof(Category).GetProperty(nameof(Category.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(category, id);
        return category;
    }

    [Fact]
    public async Task Sem_filtro_retorna_todas_com_natureza_externa()
    {
        var repository = new FakeRepository(Stored(1, "Salário", CategoryNature.Income), Stored(2, "Mercado", CategoryNature.Expense));

        var result = await new ListCategoriesHandler(repository).HandleAsync(new ListCategoriesQuery(null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [new CategoryResult(1, "Salário", "INCOME"), new CategoryResult(2, "Mercado", "EXPENSE")],
            result.Value!);
        Assert.Null(repository.LastFilter);
    }

    [Fact]
    public async Task Filtro_valido_e_traduzido_para_o_dominio()
    {
        var repository = new FakeRepository(Stored(1, "Salário", CategoryNature.Income), Stored(2, "Mercado", CategoryNature.Expense));

        var result = await new ListCategoriesHandler(repository).HandleAsync(new ListCategoriesQuery("EXPENSE"), CancellationToken.None);

        Assert.Equal(CategoryNature.Expense, repository.LastFilter);
        Assert.Equal([new CategoryResult(2, "Mercado", "EXPENSE")], result.Value!);
    }

    [Fact]
    public async Task Repositorio_vazio_retorna_lista_vazia()
    {
        var result = await new ListCategoriesHandler(new FakeRepository()).HandleAsync(new ListCategoriesQuery(null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("income")]
    [InlineData("RECEITA")]
    public async Task Filtro_invalido_retorna_erro_de_nature_sem_consultar_o_repositorio(string nature)
    {
        var repository = new FakeRepository();

        var result = await new ListCategoriesHandler(repository).HandleAsync(new ListCategoriesQuery(nature), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("nature", result.ValidationErrors!.Keys);
        Assert.Equal(0, repository.ListCalls);
    }

    [Fact]
    public async Task Token_cancelado_interrompe_antes_de_consultar()
    {
        var repository = new FakeRepository();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ListCategoriesHandler(repository).HandleAsync(new ListCategoriesQuery(null), cts.Token));
        Assert.Equal(0, repository.ListCalls);
    }
}
