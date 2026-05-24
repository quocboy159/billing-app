using Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Products;

internal sealed class ProductServiceConfiguration : IEntityTypeConfiguration<ProductService>
{
    public void Configure(EntityTypeBuilder<ProductService> b)
    {
        b.ToTable("ProductServices");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(250).IsRequired();
    }
}
