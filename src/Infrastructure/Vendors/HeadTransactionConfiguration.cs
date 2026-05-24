using Domain.Vendors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Vendors;

internal sealed class HeadTransactionConfiguration : IEntityTypeConfiguration<HeadTransaction>
{
    public void Configure(EntityTypeBuilder<HeadTransaction> b)
    {
        b.ToTable("HeadTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(250).IsRequired();
        b.Property(x => x.AccountTaxId).HasMaxLength(60);
        b.Property(x => x.Currency).HasMaxLength(3);

        b.HasIndex(x => new { x.CompanyId, x.SubHeadsId });
    }
}
