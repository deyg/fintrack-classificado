using FinTrack.Domain.Common;

namespace FinTrack.Domain.Categories;

public sealed class Category
{
    public const int NameMaxLength = 100;

    private Category(string name, CategoryNature nature)
    {
        Name = name;
        Nature = nature;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    public CategoryNature Nature { get; private set; }

    public static Category Create(string? name, CategoryNature nature) =>
        new(NormalizeName(name), EnsureValidNature(nature));

    public void Rename(string? name) => Name = NormalizeName(name);

    public void ChangeNature(CategoryNature nature) => Nature = EnsureValidNature(nature);

    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException("category.name.required", "O nome da categoria é obrigatório.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException(
                "category.name.too_long",
                $"O nome da categoria deve ter no máximo {NameMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static CategoryNature EnsureValidNature(CategoryNature nature) =>
        Enum.IsDefined(nature)
            ? nature
            : throw new DomainException("category.nature.invalid", "A natureza da categoria deve ser receita ou despesa.");
}
