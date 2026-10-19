using FinTrack.Application;
using FinTrack.Application.Features.Accounts;
using FinTrack.Application.Features.Transactions;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;
using FinTrack.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Tests.Persistence;

public sealed class TransactionPersistenceIntegrationTests(SqlServerDatabaseFixture database)
    : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private static readonly DateOnly Date = new(2026, 10, 6);

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

    private async Task<int> SeedAccountAsync()
    {
        await using var context = database.CreateContext();
        var category = Category.Create($"Cat-{Guid.NewGuid():N}", CategoryNature.Expense);
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var account = Account.Create(category.Id, "Conta", CategoryNature.Expense, ExpenseClassifier.OF);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("1800.50")]
    [InlineData("9999999999999999.99")]
    public async Task Roundtrip_preserva_valor_data_e_descricao(string amountText)
    {
        var amount = decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture);
        var accountId = await SeedAccountAsync();
        await using var provider = BuildProvider();
        int id;
        await using (var scope = provider.CreateAsyncScope())
        {
            var transaction = Transaction.Create(accountId, amount, Date, "Aluguel");
            await scope.ServiceProvider.GetRequiredService<ITransactionRepository>().AddAsync(transaction, CancellationToken.None);
            id = transaction.Id;
        }

        await using var reader = provider.CreateAsyncScope();
        var stored = await reader.ServiceProvider.GetRequiredService<ITransactionRepository>().GetByIdAsync(id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(amount, stored.Amount);
        Assert.Equal(Date, stored.RealizedAt);
        Assert.Equal("Aluguel", stored.Description);
        Assert.Equal(accountId, stored.AccountId);
    }

    [Fact]
    public async Task Update_e_delete_persistem()
    {
        var accountId = await SeedAccountAsync();
        await using var provider = BuildProvider();
        int id;
        await using (var scope = provider.CreateAsyncScope())
        {
            var transaction = Transaction.Create(accountId, 10m, Date, null);
            await scope.ServiceProvider.GetRequiredService<ITransactionRepository>().AddAsync(transaction, CancellationToken.None);
            id = transaction.Id;
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var transaction = (await repository.GetByIdAsync(id, CancellationToken.None))!;
            transaction.Change(accountId, 25.75m, Date.AddDays(-1), "Ajuste");
            await repository.UpdateAsync(transaction, CancellationToken.None);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var stored = (await repository.GetByIdAsync(id, CancellationToken.None))!;
            Assert.Equal(25.75m, stored.Amount);
            Assert.Equal(Date.AddDays(-1), stored.RealizedAt);
            Assert.True(await repository.DeleteAsync(id, CancellationToken.None));
            Assert.False(await repository.DeleteAsync(id, CancellationToken.None));
            Assert.Null(await repository.GetByIdAsync(id, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Conta_inexistente_e_rejeitada_pela_fk()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var transaction = Transaction.Create(999_999, 10m, Date, null);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            scope.ServiceProvider.GetRequiredService<ITransactionRepository>().AddAsync(transaction, CancellationToken.None));
    }

    [Fact]
    public async Task Conta_com_lancamentos_nao_pode_ser_removida_pelo_banco_sem_cascade()
    {
        var accountId = await SeedAccountAsync();
        await using var provider = BuildProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITransactionRepository>()
                .AddAsync(Transaction.Create(accountId, 10m, Date, null), CancellationToken.None);
        }

        await using var scopeDelete = provider.CreateAsyncScope();
        var accounts = scopeDelete.ServiceProvider.GetRequiredService<IAccountRepository>();

        var ex = await Assert.ThrowsAsync<SqlException>(() => accounts.DeleteAsync(accountId, CancellationToken.None));

        Assert.Equal(547, ex.Number);
        Assert.NotNull(await accounts.GetByIdAsync(accountId, CancellationToken.None));
    }

    [Fact]
    public async Task Verificador_de_historico_consulta_o_banco()
    {
        var withTransactions = await SeedAccountAsync();
        var without = await SeedAccountAsync();
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ITransactionRepository>()
            .AddAsync(Transaction.Create(withTransactions, 10m, Date, null), CancellationToken.None);
        var checker = scope.ServiceProvider.GetRequiredService<IAccountHistoryChecker>();

        Assert.True(await checker.HasTransactionsAsync(withTransactions, CancellationToken.None));
        Assert.False(await checker.HasTransactionsAsync(without, CancellationToken.None));
    }

    [Fact]
    public async Task Check_constraint_rejeita_valor_nao_positivo_gravado_diretamente()
    {
        var accountId = await SeedAccountAsync();
        await using var context = database.CreateContext();

        var ex = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Transactions (AccountId, Amount, RealizedAt) VALUES ({accountId}, 0, '2026-10-06')"));

        Assert.Equal(547, ex.Number);
    }

    [Fact]
    public async Task Esquema_tem_tipos_e_indice_esperados()
    {
        await using var context = database.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var columns = connection.CreateCommand();
        columns.CommandText = """
            SELECT COLUMN_NAME, DATA_TYPE, NUMERIC_PRECISION, NUMERIC_SCALE
            FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Transactions' AND COLUMN_NAME IN ('Amount', 'RealizedAt')
            """;
        var found = new Dictionary<string, (string Type, int? Precision, int? Scale)>();
        await using (var reader = await columns.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                found[reader.GetString(0)] = (
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : Convert.ToInt32(reader.GetValue(2)),
                    reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)));
            }
        }

        Assert.Equal(("decimal", 18, 2), found["Amount"]);
        Assert.Equal("date", found["RealizedAt"].Type);

        await using var indexes = connection.CreateCommand();
        indexes.CommandText = "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('Transactions') AND name = 'IX_Transactions_AccountId_RealizedAt'";
        Assert.Equal(1, Convert.ToInt32(await indexes.ExecuteScalarAsync()));
    }
}
