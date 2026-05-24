using Domain.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Expenses;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.ToTable("Expenses");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.CompanyId, x.ExpenseDate });
    }
}

internal sealed class ExpenseTransactionConfiguration : IEntityTypeConfiguration<ExpenseTransaction>
{
    public void Configure(EntityTypeBuilder<ExpenseTransaction> b)
    {
        b.ToTable("ExpenseTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType("decimal(18,4)");
        b.Property(x => x.InvoiceAmount).HasColumnType("decimal(18,4)");
        b.HasIndex(x => x.ExpenseId);
        b.HasIndex(x => new { x.CompanyId, x.TransactionHeadId });
    }
}
