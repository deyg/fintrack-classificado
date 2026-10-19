using FinTrack.Domain.Categories;
using FinTrack.Domain.Common;

namespace FinTrack.Tests.Categories;

public sealed class CategoryTests
{
    [Theory]
    [InlineData("Moradia", CategoryNature.Expense)]
    [InlineData("Salário", CategoryNature.Income)]
    public void Create_with_valid_data_keeps_name_and_nature(string name, CategoryNature nature)
    {
        var category = Category.Create(name, nature);

        Assert.Equal(name, category.Name);
        Assert.Equal(nature, category.Nature);
        Assert.Equal(0, category.Id);
    }

    [Fact]
    public void Create_trims_edge_whitespace()
    {
        var category = Category.Create("  Moradia \t", CategoryNature.Expense);

        Assert.Equal("Moradia", category.Name);
    }

    [Fact]
    public void Create_accepts_name_with_exactly_the_maximum_length()
    {
        var name = new string('a', Category.NameMaxLength);

        Assert.Equal(name, Category.Create(name, CategoryNature.Income).Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_rejects_empty_or_blank_name(string? name)
    {
        var ex = Assert.Throws<DomainException>(() => Category.Create(name, CategoryNature.Expense));

        Assert.Equal("category.name.required", ex.Code);
    }

    [Fact]
    public void Create_rejects_name_longer_than_the_maximum()
    {
        var name = new string('a', Category.NameMaxLength + 1);

        var ex = Assert.Throws<DomainException>(() => Category.Create(name, CategoryNature.Expense));

        Assert.Equal("category.name.too_long", ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Create_rejects_undefined_nature(int value)
    {
        var ex = Assert.Throws<DomainException>(() => Category.Create("Moradia", (CategoryNature)value));

        Assert.Equal("category.nature.invalid", ex.Code);
    }

    [Fact]
    public void Nature_enum_has_exactly_income_and_expense_with_distinct_values()
    {
        Assert.Equal([CategoryNature.Income, CategoryNature.Expense], Enum.GetValues<CategoryNature>());
        Assert.NotEqual(CategoryNature.Income, CategoryNature.Expense);
        Assert.Equal(default, (CategoryNature)0);
        Assert.False(Enum.IsDefined(default(CategoryNature)));
    }
}
