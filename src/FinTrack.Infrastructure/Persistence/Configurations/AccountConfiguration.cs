using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", table =>
            table.HasCheckConstraint("CK_Accounts_Classifier", "[Classifier] IS NULL OR [Classifier] IN (1, 2, 3, 4)"));

        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedOnAdd();

        builder.Property(account => account.CategoryId).IsRequired();

        builder.Property(account => account.Name)
            .HasMaxLength(Account.NameMaxLength)
            .IsRequired();

        builder.Property(account => account.Classifier).HasConversion<int?>();

        builder.Property(account => account.Active).IsRequired();

        // RN10: sem exclusão em cascata; excluir categoria com contas é rejeitado pelo banco.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(account => account.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(account => account.CategoryId);
    }
}
