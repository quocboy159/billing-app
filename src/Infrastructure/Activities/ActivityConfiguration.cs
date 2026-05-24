using Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Activities;

internal sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> b)
    {
        b.ToTable("Activities");
        b.HasKey(x => x.Id);
        b.Property(x => x.TableTransactionValue).HasMaxLength(100);
        b.Property(x => x.TransactionHeadName).HasMaxLength(250);
        b.Property(x => x.Remarks).HasMaxLength(500);
        b.Property(x => x.UserName).HasMaxLength(256);
        b.Property(x => x.Created).HasDefaultValueSql("GETUTCDATE()");

        b.HasIndex(x => new { x.CompanyId, x.TableEnumType, x.TableTransactionId });
    }
}
