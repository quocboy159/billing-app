using Domain.Bills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Bills;

internal sealed class RecurringBillMapConfiguration : IEntityTypeConfiguration<RecurringBillMap>
{
    public void Configure(EntityTypeBuilder<RecurringBillMap> b)
    {
        b.ToTable("RecurringBillMaps");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.BillId).IsUnique();
    }
}
