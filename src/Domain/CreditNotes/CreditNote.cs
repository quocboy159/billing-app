namespace Domain.CreditNotes;

public sealed class CreditNote
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int InvoiceId { get; set; }
    public int Status { get; set; }
    public DateTime CreditNoteDate { get; set; }
}
