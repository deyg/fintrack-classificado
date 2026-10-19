using FinTrack.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", table =>
            table.HasCheckConstraint("CK_Categories_Nature", "[Nature] IN (1, 2)"));

        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedOnAdd();

        builder.Property(category => category.Name)
            .HasMaxLength(Category.NameMaxLength)
            .IsRequired();

        builder.Property(category => category.Nature)
            .HasConversion<int>()
            .IsRequired();
    }
}
