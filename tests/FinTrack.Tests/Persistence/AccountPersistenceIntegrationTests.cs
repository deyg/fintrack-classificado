using FinTrack.Application;
using FinTrack.Application.Features.Accounts;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Tests.Persistence;

public sealed class AccountPersistenceIntegrationTests(SqlServerDatabaseFixture database)
    : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddApplication()
            .AddInfrastructure(database.ConnectionString)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    private async Task<int> SeedCategoryAsync(string name, CategoryNature nature)
    {
        await using var context = database.CreateContext();
        var category = Category.Create(name, nature);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    [Theory]
    [InlineData(CategoryNature.Expense, ExpenseClassifier.NOV)]
    [InlineData(CategoryNature.Income, null)]
    public async Task Repositorio_insere_conta_valida_e_leitura_real_a_devolve(CategoryNature nature, ExpenseClassifier? classifier)
    {
        var categoryId = await SeedCategoryAsync($"Cat-{Guid.NewGuid():N}", nature);
        await using var provider = BuildProvider();
        int id;
        await using (var scope = provider.CreateAsyncScope())
        {
            var account = Account.Create(categoryId, "Conta teste", nature, classifier);
            await scope.ServiceProvider.GetRequiredService<IAccountRepository>().AddAsync(account, CancellationToken.None);
            id = account.Id;
        }

        await using var reader = provider.CreateAsyncScope();
        var stored = await reader.ServiceProvider.GetRequiredService<IAccountRepository>().GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(categoryId, stored.CategoryId);
        Assert.Equal("Conta teste", stored.Name);
        Assert.Equal(classifier, stored.Classifier);
        Assert.True(stored.Active);
    }

    [Fact]
    public async Task Desativacao_persiste_e_preserva_os_demais_dados()
    {
        var categoryId = await SeedCategoryAsync($"Cat-{Guid.NewGuid():N}", CategoryNature.Expense);
        int id;
        await using (var context = database.CreateContext())
        {
            var account = Account.Create(categoryId, "Aluguel", CategoryNature.Expense, ExpenseClassifier.OF);
            account.Deactivate();
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            id = account.Id;
        }

        await using var reader = database.CreateContext();
        var stored = await reader.Accounts.AsNoTracking().SingleAsync(a => a.Id == id);
        Assert.False(stored.Active);
        Assert.Equal(ExpenseClassifier.OF, stored.Classifier);
    }

    [Fact]
    public async Task Categoria_inexistente_e_rejeitada_pela_fk_sem_inserir()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var account = Account.Create(987654, "Orfa", CategoryNature.Income, null);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            scope.ServiceProvider.GetRequiredService<IAccountRepository>().AddAsync(account, CancellationToken.None));

        await using var reader = database.CreateContext();
        Assert.False(await reader.Accounts.AnyAsync(a => a.Name == "Orfa"));
    }

    [Fact]
    public async Task Excluir_categoria_com_conta_e_rejeitado_sem_cascata_e_preserva_a_categoria()
    {
        var categoryId = await SeedCategoryAsync($"Cat-{Guid.NewGuid():N}", CategoryNature.Income);
        await using (var context = database.CreateContext())
        {
            context.Accounts.Add(Account.Create(categoryId, "Salário", CategoryNature.Income, null));
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            var category = await context.Categories.SingleAsync(c => c.Id == categoryId);
            context.Categories.Remove(category);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using var reader = database.CreateContext();
        Assert.True(await reader.Categories.AnyAsync(c => c.Id == categoryId));
        Assert.True(await reader.Accounts.AnyAsync(a => a.CategoryId == categoryId));
    }

    [Fact]
    public async Task Excluir_categoria_sem_contas_continua_permitido()
    {
        var categoryId = await SeedCategoryAsync($"Cat-{Guid.NewGuid():N}", CategoryNature.Expense);

        await using var context = database.CreateContext();
        context.Categories.Remove(await context.Categories.SingleAsync(c => c.Id == categoryId));
        await context.SaveChangesAsync();

        Assert.False(await context.Categories.AnyAsync(c => c.Id == categoryId));
    }

    [Fact]
    public async Task Banco_rejeita_classificador_invalido_por_check_constraint()
    {
        var categoryId = await SeedCategoryAsync($"Cat-{Guid.NewGuid():N}", CategoryNature.Expense);
        await using var context = database.CreateContext();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Accounts (CategoryId, Name, Classifier, Active) VALUES ({0}, 'X', 9, 1)", categoryId));
    }

    [Fact]
    public async Task Token_cancelado_nao_insere_a_conta()
    {
        var categoryId = await SeedCategoryAsync($"Cat-{Guid.NewGuid():N}", CategoryNature.Income);
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<IAccountRepository>()
                .AddAsync(Account.Create(categoryId, "Cancelada", CategoryNature.Income, null), cts.Token));

        await using var reader = database.CreateContext();
        Assert.False(await reader.Accounts.AnyAsync(a => a.Name == "Cancelada"));
    }
}
