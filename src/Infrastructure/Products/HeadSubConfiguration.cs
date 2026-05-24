using Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Products;

internal sealed class HeadSubConfiguration : IEntityTypeConfiguration<HeadSub>
{
    public void Configure(EntityTypeBuilder<HeadSub> b)
    {
        b.ToTable("HeadSubs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(250).IsRequired();
        b.HasIndex(x => x.HeadId);
    }
}
