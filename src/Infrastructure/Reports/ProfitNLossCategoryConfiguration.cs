using Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Reports;

internal sealed class ProfitNLossCategoryConfiguration : IEntityTypeConfiguration<ProfitNLossCategory>
{
    public void Configure(EntityTypeBuilder<ProfitNLossCategory> b)
    {
        b.ToTable("tblProfitNLoss");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.SubHeadId).IsUnique();
    }
}
