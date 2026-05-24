namespace Domain.Products;

public sealed class ProductService
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? HeadTransactionId { get; set; }
}
