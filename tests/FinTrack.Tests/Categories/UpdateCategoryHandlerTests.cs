using System.Reflection;
using FinTrack.Application.Features.Categories;
using FinTrack.Application.Features.Categories.UpdateCategory;
using FinTrack.Domain.Categories;

namespace FinTrack.Tests.Categories;

public sealed class UpdateCategoryHandlerTests
{
    private sealed class FakeRepository(Category? stored, bool hasAccounts = false) : ICategoryRepository
    {
        public int Updates { get; private set; }

        public int AccountChecks { get; private set; }

        public Task AddAsync(Category category, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Category>> ListAsync(CategoryNature? nature, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Category>>([]);

        public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(stored);

        public Task UpdateAsync(Category category, CancellationToken cancellationToken)
        {
            Updates++;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> HasAccountsAsync(int categoryId, CancellationToken cancellationToken)
        {
            AccountChecks++;
            return Task.FromResult(hasAccounts);
        }
    }

    private static Category Stored(string name = "Mercado", CategoryNature nature = CategoryNature.Expense)
    {
        var category = Category.Create(name, nature);
        typeof(Category).GetProperty(nameof(Category.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(category, 5);
        return category;
    }

    private static UpdateCategoryHandler Handler(FakeRepository repository) => new(new UpdateCategoryValidator(), repository);

    [Fact]
    public async Task Renomeia_categoria_sem_vinculos()
    {
        var repository = new FakeRepository(Stored());

        var result = await Handler(repository).HandleAsync(new UpdateCategoryCommand(5, "  Supermercado ", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new CategoryResult(5, "Supermercado", "EXPENSE"), result.Value);
        Assert.Equal(1, repository.Updates);
    }

    [Fact]
    public async Task Altera_natureza_sem_vinculos()
    {
        var repository = new FakeRepository(Stored());

        var result = await Handler(repository).HandleAsync(new UpdateCategoryCommand(5, null, "INCOME"), CancellationToken.None);

        Assert.Equal("INCOME", result.Value!.Nature);
        Assert.Equal(1, repository.Updates);
    }

    [Fact]
    public async Task Altera_nome_e_natureza_juntos()
    {
        var result = await Handler(new FakeRepository(Stored())).HandleAsync(
            new UpdateCategoryCommand(5, "Bônus", "INCOME"), CancellationToken.None);

        Assert.Equal(new CategoryResult(5, "Bônus", "INCOME"), result.Value);
    }

    [Theory]
    [InlineData("Mercado", null)]
    [InlineData(null, "EXPENSE")]
    [InlineData(" Mercado ", "EXPENSE")]
    public async Task Sem_mudanca_efetiva_retorna_sucesso_sem_gravar_nem_consultar_vinculos(string? name, string? nature)
    {
        var repository = new FakeRepository(Stored(), hasAccounts: true);

        var result = await Handler(repository).HandleAsync(new UpdateCategoryCommand(5, name, nature), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, repository.Updates);
        Assert.Equal(0, repository.AccountChecks);
    }

    [Theory]
    [InlineData("Outro nome", null)]
    [InlineData(null, "INCOME")]
    [InlineData("Outro", "INCOME")]
    public async Task Com_contas_vinculadas_renomear_ou_mudar_natureza_gera_conflito_sem_gravar(string? name, string? nature)
    {
        var repository = new FakeRepository(Stored(), hasAccounts: true);

        var result = await Handler(repository).HandleAsync(new UpdateCategoryCommand(5, name, nature), CancellationToken.None);

        Assert.True(result.IsConflict);
        Assert.False(result.IsSuccess);
        Assert.Equal(0, repository.Updates);
    }

    [Fact]
    public async Task Inexistente_retorna_not_found()
    {
        var repository = new FakeRepository(null);

        var result = await Handler(repository).HandleAsync(new UpdateCategoryCommand(5, "X", null), CancellationToken.None);

        Assert.True(result.IsNotFound);
        Assert.Equal(0, repository.Updates);
    }

    [Theory]
    [InlineData(null, null, "body")]
    [InlineData("", null, "name")]
    [InlineData("   ", null, "name")]
    [InlineData(null, "RECEITA", "nature")]
    [InlineData(null, "income", "nature")]
    public async Task Valores_invalidos_retornam_erro_por_campo_sem_consultar_o_repositorio(string? name, string? nature, string field)
    {
        var repository = new FakeRepository(Stored(), hasAccounts: true);

        var result = await Handler(repository).HandleAsync(new UpdateCategoryCommand(5, name, nature), CancellationToken.None);

        Assert.Contains(field, result.ValidationErrors!.Keys);
        Assert.Equal(0, repository.Updates);
        Assert.Equal(0, repository.AccountChecks);
    }

    [Fact]
    public async Task Nome_com_101_caracteres_e_invalido_e_com_100_e_aceito()
    {
        var repository = new FakeRepository(Stored());

        var tooLong = await Handler(repository).HandleAsync(new UpdateCategoryCommand(5, new string('a', 101), null), CancellationToken.None);
        var limit = await Handler(repository).HandleAsync(new UpdateCategoryCommand(5, new string('b', 100), null), CancellationToken.None);

        Assert.Contains("name", tooLong.ValidationErrors!.Keys);
        Assert.True(limit.IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Id_menor_que_1_retorna_erro_de_id(int id)
    {
        var result = await Handler(new FakeRepository(Stored())).HandleAsync(new UpdateCategoryCommand(id, "X", null), CancellationToken.None);

        Assert.Contains(UpdateCategoryHandler.IdField, result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Token_cancelado_interrompe_antes_de_qualquer_trabalho()
    {
        var repository = new FakeRepository(Stored());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Handler(repository).HandleAsync(new UpdateCategoryCommand(5, "X", null), cts.Token));
        Assert.Equal(0, repository.Updates);
    }
}
