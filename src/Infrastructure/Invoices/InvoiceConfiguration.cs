using Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Invoices;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("Invoices");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType("decimal(18,4)");
        b.Property(x => x.Discount).HasColumnType("decimal(18,4)");
        b.HasIndex(x => new { x.CompanyId, x.Status, x.InvoiceDate });
    }
}

internal sealed class InvoiceTransactionConfiguration : IEntityTypeConfiguration<InvoiceTransaction>
{
    public void Configure(EntityTypeBuilder<InvoiceTransaction> b)
    {
        b.ToTable("InvoiceTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        b.Property(x => x.Price).HasColumnType("decimal(18,10)");
        b.Property(x => x.Discount).HasColumnType("decimal(18,4)");
        b.HasIndex(x => x.InvoiceId);
    }
}

internal sealed class InvoicePaymentDetailConfiguration : IEntityTypeConfiguration<InvoicePaymentDetail>
{
    public void Configure(EntityTypeBuilder<InvoicePaymentDetail> b)
    {
        b.ToTable("InvoicePaymentDetails");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType("decimal(18,4)");
        b.HasIndex(x => x.InvoiceId);
    }
}
