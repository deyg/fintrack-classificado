using FinTrack.Application.Features.Transactions;
using FinTrack.Application.Features.Transactions.ListTransactions;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;

namespace FinTrack.Tests.Transactions;

public sealed class ListTransactionsHandlerTests
{
    private sealed class FakeTransactions(int total = 0) : ITransactionRepository
    {
        public TransactionFilter? Filter { get; private set; }

        public int Page { get; private set; }

        public int PageSize { get; private set; }

        public int Calls { get; private set; }

        public Task AddAsync(Transaction transaction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<Transaction?>(null);

        public Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<TransactionPageData> ListAsync(
            TransactionFilter filter, int page, int pageSize, CancellationToken cancellationToken)
        {
            Calls++;
            Filter = filter;
            Page = page;
            PageSize = pageSize;
            return Task.FromResult(new TransactionPageData([], total));
        }
    }

    private static ListTransactionsQuery Query(
        int? accountId = null, int? categoryId = null, string? nature = null, string? classifier = null,
        DateOnly? from = null, DateOnly? to = null, int? page = null, int? pageSize = null) =>
        new(accountId, categoryId, nature, classifier, from, to, page, pageSize);

    [Fact]
    public async Task Aplica_defaults_e_traduz_filtros()
    {
        var repository = new FakeTransactions(41);

        var result = await new ListTransactionsHandler(repository).HandleAsync(
            Query(1, 2, "EXPENSE", "OF", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.Page);
        Assert.Equal(20, repository.PageSize);
        Assert.Equal(CategoryNature.Expense, repository.Filter!.Nature);
        Assert.Equal(ExpenseClassifier.OF, repository.Filter.Classifier);
        Assert.Equal(3, result.Value!.Pagination.TotalPages);
        Assert.Equal(41, result.Value.Pagination.TotalItems);
    }

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(21, 20, 2, 2)]
    [InlineData(20, 20, 1, 1)]
    [InlineData(0, 20, 1, 0)]
    public void Total_de_paginas_arredonda_para_cima(int total, int size, int page, int expectedPages)
    {
        Assert.Equal(expectedPages, FinTrack.Application.Common.PaginationResult.Create(page, size, total).TotalPages);
    }

    [Theory]
    [InlineData(0, null, "page")]
    [InlineData(null, 0, "pageSize")]
    [InlineData(null, 101, "pageSize")]
    public async Task Pagina_fora_dos_limites_e_rejeitada_sem_consultar(int? page, int? pageSize, string field)
    {
        var repository = new FakeTransactions();

        var result = await new ListTransactionsHandler(repository)
            .HandleAsync(Query(page: page, pageSize: pageSize), CancellationToken.None);

        Assert.Contains(field, result.ValidationErrors!.Keys);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task Filtros_invalidos_sao_rejeitados()
    {
        var result = await new ListTransactionsHandler(new FakeTransactions())
            .HandleAsync(Query(0, 0, "X", "Y"), CancellationToken.None);

        Assert.Equal(
            ["accountId", "categoryId", "classifier", "nature"],
            result.ValidationErrors!.Keys.Order().ToArray());
    }

    [Fact]
    public async Task Periodo_invertido_gera_apenas_o_erro_de_periodo()
    {
        var result = await new ListTransactionsHandler(new FakeTransactions())
            .HandleAsync(Query(from: new DateOnly(2026, 2, 1), to: new DateOnly(2026, 1, 1)), CancellationToken.None);

        Assert.Equal([ListTransactionsHandler.PeriodField], result.ValidationErrors!.Keys.ToArray());
    }

    [Fact]
    public async Task Periodo_de_um_dia_e_valido()
    {
        var day = new DateOnly(2026, 2, 1);

        var result = await new ListTransactionsHandler(new FakeTransactions()).HandleAsync(Query(from: day, to: day), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Token_cancelado_interrompe()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ListTransactionsHandler(new FakeTransactions()).HandleAsync(Query(), cts.Token));
    }
}
