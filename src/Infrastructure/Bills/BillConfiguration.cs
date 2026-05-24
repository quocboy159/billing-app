using Domain.Bills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Bills;

internal sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> b)
    {
        b.ToTable("Bills");
        b.HasKey(x => x.Id);
        b.Property(x => x.Guid).HasDefaultValueSql("NEWID()");
        b.Property(x => x.InvoiceNo).HasMaxLength(50).IsRequired();
        b.Property(x => x.PoNumber).HasMaxLength(50);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.Price).HasColumnType("decimal(18,4)");
        b.Property(x => x.ExchangeRate).HasColumnType("decimal(18,6)");
        b.Property(x => x.Created).HasDefaultValueSql("GETUTCDATE()");

        // Tell EF that the LineItems collection is the canonical navigation, backed by the
        // private _lineItems field. PropertyAccessMode.Field lets EF write directly to the
        // backing field on materialisation.
        b.HasMany(x => x.LineItems)
            .WithOne()
            .HasForeignKey(t => t.BillId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Metadata
            .FindNavigation(nameof(Bill.LineItems))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => new { x.CompanyId, x.Status, x.IsArchive, x.Created });
        b.HasIndex(x => new { x.Guid, x.CompanyId }).IsUnique();
        b.HasIndex(x => new { x.CompanyId, x.HeadTransactionVendorId });
    }
}
