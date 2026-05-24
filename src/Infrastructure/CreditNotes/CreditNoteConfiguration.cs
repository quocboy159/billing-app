using Domain.CreditNotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.CreditNotes;

internal sealed class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> b)
    {
        b.ToTable("CreditNotes");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.CompanyId, x.Status, x.CreditNoteDate });
        b.HasIndex(x => x.InvoiceId);
    }
}

internal sealed class CreditNoteTransactionConfiguration : IEntityTypeConfiguration<CreditNoteTransaction>
{
    public void Configure(EntityTypeBuilder<CreditNoteTransaction> b)
    {
        b.ToTable("CreditNoteTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Property(x => x.Price).HasColumnType("decimal(18,10)");
        b.Property(x => x.Discount).HasColumnType("decimal(18,4)");
        b.HasIndex(x => x.CreditNoteId);
    }
}
