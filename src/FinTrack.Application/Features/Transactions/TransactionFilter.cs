using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;

namespace FinTrack.Application.Features.Transactions;

// Filtros combinados por AND; nature e classifier derivam da categoria/conta vinculadas (RN08).
public sealed record TransactionFilter(
    int? AccountId,
    int? CategoryId,
    CategoryNature? Nature,
    ExpenseClassifier? Classifier,
    DateOnly? From,
    DateOnly? To);

public sealed record TransactionPageData(IReadOnlyList<Transaction> Items, int TotalItems);
