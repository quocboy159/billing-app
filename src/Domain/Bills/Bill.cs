using SharedKernel;

namespace Domain.Bills;

public sealed class Bill : Entity
{
    private readonly List<BillTransaction> _lineItems = [];

    public int Id { get; set; }
    public Guid Guid { get; set; }
    public int CompanyId { get; set; }
    public int HeadTransactionVendorId { get; set; }
    public BillStatus Status { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string? PoNumber { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal Price { get; set; }
    public DateTime BillAt { get; set; }
    public DateTime DueAt { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Deleted { get; set; }
    public bool IsArchive { get; set; }

    /// <summary>
    /// Line items belonging to this bill. EF treats this as the canonical collection navigation,
    /// which lets us call <see cref="AddLine"/> inside a single SaveChanges and have FK fixup
    /// (BillId, BillTransactionId on Taxes) wired up automatically.
    /// </summary>
    public IReadOnlyList<BillTransaction> LineItems => _lineItems;

    /// <summary>
    /// Factory for a brand-new draft bill. Encapsulates the invariants the SP's BillsAdd
    /// enforces inline: new Guid, Status = Draft, IsArchive = false, Created = now, and a
    /// <see cref="BillCreatedDomainEvent"/> raised so downstream listeners can react.
    /// </summary>
    public static Bill CreateDraft(
        int companyId,
        int vendorHeadId,
        string invoiceNo,
        decimal price,
        decimal exchangeRate,
        DateTime billAt,
        DateTime dueAt,
        DateTime utcNow,
        string? currency = null,
        string? poNumber = null,
        string? notes = null)
    {
        var bill = new Bill
        {
            Guid = Guid.NewGuid(),
            CompanyId = companyId,
            HeadTransactionVendorId = vendorHeadId,
            Status = BillStatus.Draft,
            InvoiceNo = invoiceNo,
            Price = price,
            ExchangeRate = exchangeRate,
            BillAt = billAt,
            DueAt = dueAt,
            Currency = currency,
            PoNumber = poNumber,
            Notes = notes,
            Created = utcNow,
            IsArchive = false
        };

        bill.Raise(new BillCreatedDomainEvent(bill.Guid, bill.CompanyId, bill.InvoiceNo));
        return bill;
    }

    /// <summary>
    /// Adds a line item. EF's relationship fixup picks up the new <see cref="BillTransaction"/>
    /// from the navigation collection, so the caller does not need to set <c>BillId</c> manually.
    /// Returns the new line so the caller can attach taxes via <see cref="BillTransaction.AddTax"/>.
    /// </summary>
    public BillTransaction AddLine(
        int productId,
        int productTransactionHeadId,
        decimal quantity,
        decimal price,
        string? description = null)
    {
        var line = new BillTransaction
        {
            CompanyId = CompanyId,
            ProductId = productId,
            ProductTransactionHeadId = productTransactionHeadId,
            Quantity = quantity,
            Price = price,
            Description = description
        };
        _lineItems.Add(line);
        return line;
    }

    /// <summary>
    /// Domain rule: a bill that uses a non-empty currency different from the company's base
    /// currency requires the multi-currency policy gate (SP plan type = 24).
    /// </summary>
    public static bool RequiresMultiCurrencyApproval(string? billCurrency, string? companyCurrency) =>
        !string.IsNullOrEmpty(billCurrency)
        && !string.Equals(companyCurrency, billCurrency, StringComparison.OrdinalIgnoreCase);
}
