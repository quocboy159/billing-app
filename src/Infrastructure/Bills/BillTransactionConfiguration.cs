using Domain.Bills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Bills;

internal sealed class BillTransactionConfiguration : IEntityTypeConfiguration<BillTransaction>
{
    public void Configure(EntityTypeBuilder<BillTransaction> b)
    {
        b.ToTable("BillTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Property(x => x.Price).HasColumnType("decimal(18,4)");

        b.HasMany(x => x.Taxes)
            .WithOne()
            .HasForeignKey(t => t.BillTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Metadata
            .FindNavigation(nameof(BillTransaction.Taxes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.BillId, x.CompanyId });
        b.HasIndex(x => x.ProductId);
    }
}
