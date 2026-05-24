using Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Companies;

internal sealed class PurchaseCounterConfiguration : IEntityTypeConfiguration<PurchaseCounter>
{
    public void Configure(EntityTypeBuilder<PurchaseCounter> b)
    {
        b.ToTable("PurchaseCounters");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.CompanyId).IsUnique();
    }
}
