using Domain.Vendors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Vendors;

internal sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> b)
    {
        b.ToTable("Vendors");
        b.HasKey(x => x.Id);
        b.Property(x => x.VendorName).HasMaxLength(100).IsRequired();
        b.Property(x => x.FirstName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100);
        b.Property(x => x.AccountNumber).HasMaxLength(60);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Created).HasDefaultValueSql("GETUTCDATE()");

        b.HasIndex(x => new { x.CompanyId, x.VendorName }).IsUnique();
        b.HasIndex(x => x.HeadTransactionId);
    }
}
