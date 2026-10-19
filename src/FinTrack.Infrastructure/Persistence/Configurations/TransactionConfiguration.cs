using FinTrack.Domain.Accounts;
using FinTrack.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions", table =>
            table.HasCheckConstraint("CK_Transactions_Amount", "[Amount] > 0"));

        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Id).ValueGeneratedOnAdd();

        builder.Property(transaction => transaction.AccountId).IsRequired();

        builder.Property(transaction => transaction.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(transaction => transaction.RealizedAt)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(transaction => transaction.Description)
            .HasMaxLength(Transaction.DescriptionMaxLength);

        // RN10: sem exclusão em cascata; excluir conta com lançamentos é rejeitado pelo banco.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transaction => transaction.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(transaction => new { transaction.AccountId, transaction.RealizedAt });
    }
}
