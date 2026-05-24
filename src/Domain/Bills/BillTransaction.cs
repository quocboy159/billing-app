namespace Domain.Bills;

public sealed class BillTransaction
{
    private readonly List<BillTransactionTax> _taxes = [];

    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BillId { get; set; }
    public int ProductId { get; set; }
    public string? Description { get; set; }
    public int ProductTransactionHeadId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }

    /// <summary>
    /// Taxes attached to this line. EF wires the FK (BillTransactionId) via this collection
    /// when SaveChanges runs, so taxes can be added before the parent has an Id.
    /// </summary>
    public IReadOnlyList<BillTransactionTax> Taxes => _taxes;

    public BillTransactionTax AddTax(int transactionHeadTaxId, string name, decimal rate)
    {
        var tax = new BillTransactionTax
        {
            CompanyId = CompanyId,
            TransactionHeadTaxId = transactionHeadTaxId,
            Name = name,
            Rate = rate
        };
        _taxes.Add(tax);
        return tax;
    }
}
