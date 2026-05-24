namespace Domain.CreditNotes;

public sealed class CreditNoteTransaction
{
    public int Id { get; set; }
    public int CreditNoteId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public int DiscountType { get; set; }
    public decimal Discount { get; set; }
}
