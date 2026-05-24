namespace Domain.Invoices;

public sealed class Invoice
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTime InvoiceDate { get; set; }
    public decimal Amount { get; set; }
    public int DiscountType { get; set; }
    public decimal Discount { get; set; }
}
