using FinTrack.Domain.Categories;
using FinTrack.Domain.Common;

namespace FinTrack.Domain.Accounts;

public sealed class Account
{
    public const int NameMaxLength = 100;

    private Account(int categoryId, string name, ExpenseClassifier? classifier)
    {
        CategoryId = categoryId;
        Name = name;
        Classifier = classifier;
        Active = true;
    }

    public int Id { get; private set; }

    public int CategoryId { get; private set; }

    public string Name { get; private set; }

    public ExpenseClassifier? Classifier { get; private set; }

    public bool Active { get; private set; }

    // A natureza da categoria é informada pelo caso de uso; o Domain não conhece repositórios.
    public static Account Create(
        int categoryId,
        string? name,
        CategoryNature categoryNature,
        ExpenseClassifier? classifier)
    {
        if (categoryId < 1)
        {
            throw new DomainException("account.category.required", "A conta deve pertencer a uma categoria válida.");
        }

        return new Account(categoryId, NormalizeName(name), ValidateClassifier(categoryNature, classifier));
    }

    // Idempotente: ativar uma conta ativa (ou desativar uma inativa) não é erro.
    public void Activate() => Active = true;

    public void Deactivate() => Active = false;

    public void Rename(string? name) => Name = NormalizeName(name);

    // Valida a coerência entre natureza da categoria e classificador antes de qualquer alteração.
    public static void EnsureCoherentClassification(CategoryNature nature, ExpenseClassifier? classifier) =>
        ValidateClassifier(nature, classifier);

    public void Reclassify(int categoryId, CategoryNature nature, ExpenseClassifier? classifier)
    {
        if (categoryId < 1)
        {
            throw new DomainException("account.category.required", "A conta deve pertencer a uma categoria válida.");
        }

        var validated = ValidateClassifier(nature, classifier);
        CategoryId = categoryId;
        Classifier = validated;
    }

    internal static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException("account.name.required", "O nome da conta é obrigatório.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException(
                "account.name.too_long",
                $"O nome da conta deve ter no máximo {NameMaxLength} caracteres.");
        }

        return trimmed;
    }

    internal static ExpenseClassifier? ValidateClassifier(CategoryNature nature, ExpenseClassifier? classifier)
    {
        if (!Enum.IsDefined(nature))
        {
            throw new DomainException("category.nature.invalid", "A natureza da categoria deve ser receita ou despesa.");
        }

        if (classifier is { } value && !Enum.IsDefined(value))
        {
            throw new DomainException("account.classifier.invalid", "O classificador deve ser OF, OV, NOF ou NOV.");
        }

        return nature switch
        {
            CategoryNature.Expense when classifier is null =>
                throw new DomainException("account.classifier.required", "Conta de despesa exige classificador."),
            CategoryNature.Income when classifier is not null =>
                throw new DomainException("account.classifier.not_allowed", "Conta de receita não admite classificador."),
            _ => classifier,
        };
    }
}
