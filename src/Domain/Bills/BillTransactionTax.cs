namespace Domain.Bills;

public sealed class BillTransactionTax
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BillTransactionId { get; set; }
    public int TransactionHeadTaxId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}
