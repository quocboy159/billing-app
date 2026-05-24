using Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Companies;

internal sealed class PurchaseModuleCompanySettingConfiguration : IEntityTypeConfiguration<PurchaseModuleCompanySetting>
{
    public void Configure(EntityTypeBuilder<PurchaseModuleCompanySetting> b)
    {
        b.ToTable("PurchaseModuleCompanySettings");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillNoPrefix).HasMaxLength(10);
        b.Property(x => x.BillNoSuffix).HasMaxLength(10);
        b.HasIndex(x => x.CompanyId).IsUnique();
    }
}
