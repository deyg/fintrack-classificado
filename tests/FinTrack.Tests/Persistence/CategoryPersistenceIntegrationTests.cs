using FinTrack.Application;
using FinTrack.Application.Features.Categories;
using FinTrack.Application.Features.Categories.CreateCategory;
using FinTrack.Domain.Categories;
using FinTrack.Infrastructure;
using FinTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Tests.Persistence;

public sealed class CategoryPersistenceIntegrationTests(SqlServerDatabaseFixture database)
    : IClassFixture<SqlServerDatabaseFixture>
{
    private ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddApplication()
            .AddInfrastructure(database.ConnectionString)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    private async Task MigrateAsync()
    {
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
    }

    [Fact]
    public async Task Migrate_aplicada_duas_vezes_e_idempotente()
    {
        await MigrateAsync();
        await MigrateAsync();

        await using var context = database.CreateContext();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(context.Database.GetMigrations().Count(), (await context.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Theory]
    [InlineData("Salário", CategoryNature.Income)]
    [InlineData("Mercado", CategoryNature.Expense)]
    public async Task Repositorio_insere_e_leitura_real_devolve_a_categoria(string name, CategoryNature nature)
    {
        await MigrateAsync();
        await using var provider = BuildProvider();
        int id;
        await using (var scope = provider.CreateAsyncScope())
        {
            var category = Category.Create(name, nature);
            await scope.ServiceProvider.GetRequiredService<ICategoryRepository>().AddAsync(category, CancellationToken.None);
            id = category.Id;
        }

        Assert.True(id > 0);
        await using var reader = database.CreateContext();
        var stored = await reader.Categories.AsNoTracking().SingleAsync(c => c.Id == id);
        Assert.Equal(name, stored.Name);
        Assert.Equal(nature, stored.Nature);
    }

    [Fact]
    public async Task Handler_cria_categoria_ponta_a_ponta_com_ids_distintos()
    {
        await MigrateAsync();
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateCategoryHandler>();

        var first = await handler.HandleAsync(new CreateCategoryCommand("  Lazer  ", "EXPENSE"), CancellationToken.None);
        var second = await handler.HandleAsync(new CreateCategoryCommand("Bônus", "INCOME"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal("Lazer", first.Value!.Name);
        Assert.Equal("EXPENSE", first.Value.Nature);
        Assert.NotEqual(first.Value.Id, second.Value!.Id);
        await using var reader = database.CreateContext();
        Assert.True(await reader.Categories.AnyAsync(c => c.Id == first.Value.Id && c.Name == "Lazer"));
    }

    [Fact]
    public async Task Banco_rejeita_natureza_invalida_por_check_constraint()
    {
        await MigrateAsync();
        await using var context = database.CreateContext();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Database.ExecuteSqlRawAsync("INSERT INTO Categories (Name, Nature) VALUES ('X', 9)"));
    }

    [Fact]
    public async Task Banco_limita_nome_a_100_caracteres()
    {
        await MigrateAsync();
        await using var context = database.CreateContext();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Categories (Name, Nature) VALUES (REPLICATE(N'a', 101), 1)"));
    }

    [Fact]
    public async Task Token_cancelado_interrompe_a_persistencia_sem_inserir()
    {
        await MigrateAsync();
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<ICategoryRepository>()
                .AddAsync(Category.Create("Cancelada", CategoryNature.Expense), cts.Token));

        await using var reader = database.CreateContext();
        Assert.False(await reader.Categories.AnyAsync(c => c.Name == "Cancelada"));
    }
}
