using Domain.Bills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Bills;

internal sealed class BillTransactionTaxConfiguration : IEntityTypeConfiguration<BillTransactionTax>
{
    public void Configure(EntityTypeBuilder<BillTransactionTax> b)
    {
        b.ToTable("BillTransactionTaxes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Rate).HasColumnType("decimal(18,4)");

        b.HasIndex(x => x.BillTransactionId);
    }
}
