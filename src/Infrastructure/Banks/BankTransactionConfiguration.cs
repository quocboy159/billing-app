using Domain.Banks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Banks;

internal sealed class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> b)
    {
        b.ToTable("BankTransactions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.CompanyId, x.TransactionDate });
    }
}

internal sealed class BankSubTransactionConfiguration : IEntityTypeConfiguration<BankSubTransaction>
{
    public void Configure(EntityTypeBuilder<BankSubTransaction> b)
    {
        b.ToTable("BankSubTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType("decimal(18,4)");
        b.HasIndex(x => x.BankTransactionId);
    }
}
