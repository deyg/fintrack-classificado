using FinTrack.Domain.Accounts;

namespace FinTrack.Application.Features.Accounts;

public sealed record AccountResult(int Id, int CategoryId, string Name, string? Classifier, bool Active)
{
    public static AccountResult From(Account account) =>
        new(account.Id, account.CategoryId, account.Name, ClassifierTranslator.ToExternal(account.Classifier), account.Active);
}

// Tradução entre OF/OV/NOF/NOV do contrato e o domínio (comparação sensível a maiúsculas).
public static class ClassifierTranslator
{
    public static bool TryToDomain(string? external, out ExpenseClassifier classifier)
    {
        (var ok, classifier) = external switch
        {
            "OF" => (true, ExpenseClassifier.OF),
            "OV" => (true, ExpenseClassifier.OV),
            "NOF" => (true, ExpenseClassifier.NOF),
            "NOV" => (true, ExpenseClassifier.NOV),
            _ => (false, default(ExpenseClassifier)),
        };

        return ok;
    }

    public static string? ToExternal(ExpenseClassifier? classifier) => classifier switch
    {
        null => null,
        ExpenseClassifier.OF => "OF",
        ExpenseClassifier.OV => "OV",
        ExpenseClassifier.NOF => "NOF",
        ExpenseClassifier.NOV => "NOV",
        _ => throw new ArgumentOutOfRangeException(nameof(classifier), classifier, "Classificador desconhecido."),
    };
}
