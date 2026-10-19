using FinTrack.Domain.Categories;

namespace FinTrack.Application.Features.Categories;

// Tradução entre a nomenclatura externa do contrato (INCOME/EXPENSE) e o domínio.
public static class CategoryNatureTranslator
{
    public const string Income = "INCOME";
    public const string Expense = "EXPENSE";

    public static bool TryToDomain(string? external, out CategoryNature nature)
    {
        (var ok, nature) = external switch
        {
            Income => (true, CategoryNature.Income),
            Expense => (true, CategoryNature.Expense),
            _ => (false, default(CategoryNature)),
        };

        return ok;
    }

    public static string ToExternal(CategoryNature nature) => nature switch
    {
        CategoryNature.Income => Income,
        CategoryNature.Expense => Expense,
        _ => throw new ArgumentOutOfRangeException(nameof(nature), nature, "Natureza desconhecida."),
    };
}
