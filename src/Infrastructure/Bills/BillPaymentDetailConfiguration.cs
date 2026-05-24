using Domain.Bills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Bills;

internal sealed class BillPaymentDetailConfiguration : IEntityTypeConfiguration<BillPaymentDetail>
{
    public void Configure(EntityTypeBuilder<BillPaymentDetail> b)
    {
        b.ToTable("BillPaymentDetails");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType("decimal(18,4)");

        b.HasIndex(x => x.BillId);
    }
}
