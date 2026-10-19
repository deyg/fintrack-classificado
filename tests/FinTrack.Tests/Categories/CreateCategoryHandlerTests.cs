using System.Reflection;
using FinTrack.Application;
using FinTrack.Application.Features.Categories;
using FinTrack.Application.Features.Categories.CreateCategory;
using FinTrack.Domain.Categories;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Tests.Categories;

public sealed class CreateCategoryHandlerTests
{
    private sealed class FakeRepository : ICategoryRepository
    {
        public List<Category> Added { get; } = [];

        public CancellationToken LastToken { get; private set; }

        public Exception? Failure { get; init; }

        public Task AddAsync(Category category, CancellationToken cancellationToken)
        {
            LastToken = cancellationToken;
            if (Failure is not null)
            {
                throw Failure;
            }

            typeof(Category).GetProperty(nameof(Category.Id), BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(category, 42);
            Added.Add(category);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Category>> ListAsync(CategoryNature? nature, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Category>>([]);

        public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<Category?>(null);

        public Task UpdateAsync(Category category, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> HasAccountsAsync(int categoryId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private static CreateCategoryHandler Handler(FakeRepository repository) =>
        new(new CreateCategoryValidator(), repository);

    [Theory]
    [InlineData("Moradia", "EXPENSE", "Moradia", "EXPENSE")]
    [InlineData("  Salário ", "INCOME", "Salário", "INCOME")]
    public async Task Valid_command_persists_through_the_port_and_returns_external_nature(
        string name, string nature, string expectedName, string expectedNature)
    {
        var repository = new FakeRepository();

        var result = await Handler(repository).HandleAsync(new CreateCategoryCommand(name, nature), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new CategoryResult(42, expectedName, expectedNature), result.Value);
        var added = Assert.Single(repository.Added);
        Assert.Equal(expectedName, added.Name);
    }

    [Theory]
    [InlineData(null, "EXPENSE", "name")]
    [InlineData("", "EXPENSE", "name")]
    [InlineData("   ", "INCOME", "name")]
    [InlineData("Moradia", null, "nature")]
    [InlineData("Moradia", "", "nature")]
    [InlineData("Moradia", "income", "nature")]
    [InlineData("Moradia", "RECEITA", "nature")]
    public async Task Invalid_command_returns_field_errors_and_never_calls_the_repository(
        string? name, string? nature, string expectedField)
    {
        var repository = new FakeRepository();

        var result = await Handler(repository).HandleAsync(new CreateCategoryCommand(name, nature), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(expectedField, result.ValidationErrors!.Keys);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Name_longer_than_100_after_trim_is_invalid_but_100_is_accepted()
    {
        var repository = new FakeRepository();
        var handler = Handler(repository);

        var tooLong = await handler.HandleAsync(new CreateCategoryCommand(new string('a', 101), "INCOME"), CancellationToken.None);
        var padded = await handler.HandleAsync(new CreateCategoryCommand("  " + new string('a', 100) + "  ", "INCOME"), CancellationToken.None);

        Assert.False(tooLong.IsSuccess);
        Assert.True(padded.IsSuccess);
        Assert.Single(repository.Added);
    }

    [Fact]
    public async Task Both_fields_invalid_reports_both_errors()
    {
        var result = await Handler(new FakeRepository()).HandleAsync(new CreateCategoryCommand(" ", "X"), CancellationToken.None);

        Assert.Equal(["name", "nature"], result.ValidationErrors!.Keys.Order());
    }

    [Fact]
    public async Task Cancellation_token_reaches_the_repository()
    {
        var repository = new FakeRepository();
        using var cts = new CancellationTokenSource();

        await Handler(repository).HandleAsync(new CreateCategoryCommand("Moradia", "EXPENSE"), cts.Token);

        Assert.Equal(cts.Token, repository.LastToken);
    }

    [Fact]
    public async Task Already_cancelled_token_throws_without_touching_the_repository()
    {
        var repository = new FakeRepository();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Handler(repository).HandleAsync(new CreateCategoryCommand("Moradia", "EXPENSE"), cts.Token));

        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Repository_failure_propagates()
    {
        var repository = new FakeRepository { Failure = new InvalidOperationException("falha") };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(repository).HandleAsync(new CreateCategoryCommand("Moradia", "EXPENSE"), CancellationToken.None));
    }

    [Theory]
    [InlineData("INCOME", CategoryNature.Income)]
    [InlineData("EXPENSE", CategoryNature.Expense)]
    public void Nature_translation_round_trips(string external, CategoryNature domain)
    {
        Assert.True(CategoryNatureTranslator.TryToDomain(external, out var nature));
        Assert.Equal(domain, nature);
        Assert.Equal(external, CategoryNatureTranslator.ToExternal(domain));
    }

    [Fact]
    public void Nature_translation_rejects_unknown_values()
    {
        Assert.False(CategoryNatureTranslator.TryToDomain("Income", out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CategoryNatureTranslator.ToExternal((CategoryNature)0));
    }

    [Fact]
    public void AddApplication_registers_validator_and_handler()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton<ICategoryRepository>(new FakeRepository());
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreateCategoryHandler>());
        Assert.IsType<CreateCategoryValidator>(provider.GetRequiredService<IValidator<CreateCategoryCommand>>());
    }
}
