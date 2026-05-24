namespace Domain.Invoices;

public sealed class InvoiceTransaction
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int ProductId { get; set; }
    public int ProductTransactionHeadId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public int DiscountType { get; set; }
    public decimal Discount { get; set; }
}
