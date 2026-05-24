using Domain.Bills;

namespace Application.Bills.GetByGuid;

public sealed class BillDetailResponse
{
    public BillHeader Header { get; init; } = new();
    public List<BillLine> Lines { get; init; } = [];
    public List<BillLineTax> Taxes { get; init; } = [];
    public VendorSummary? Vendor { get; init; }

    public sealed class BillHeader
    {
        public int Id { get; init; }
        public Guid Guid { get; init; }
        public int CompanyId { get; init; }
        public int HeadTransactionVendorId { get; init; }
        public BillStatus Status { get; init; }
        public string InvoiceNo { get; init; } = string.Empty;
        public string? PoNumber { get; init; }
        public decimal ExchangeRate { get; init; }
        public decimal Price { get; init; }
        public decimal DueAmount { get; init; }
        public DateTime BillAt { get; init; }
        public DateTime DueAt { get; init; }
        public string? Currency { get; init; }
        public string? Notes { get; init; }
        public DateTime Created { get; init; }
        public int PaymentDueDays { get; init; }
        public bool IsAutoCreated { get; init; }
    }

    public sealed class BillLine
    {
        public int Id { get; init; }
        public int ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int SubHeadId { get; init; }
        public string SubHeadName { get; init; } = string.Empty;
        public int ProductTransactionHeadId { get; init; }
        public string TransactionHeadName { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal Price { get; init; }
    }

    public sealed class BillLineTax
    {
        public int Id { get; init; }
        public int BillTransactionId { get; init; }
        public int TransactionHeadTaxId { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal Rate { get; init; }
    }

    public sealed class VendorSummary
    {
        public int Id { get; init; }
        public int HeadTransactionId { get; init; }
        public int CompanyId { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string VendorName { get; init; } = string.Empty;
        public string? AccountNumber { get; init; }
        public string? Email { get; init; }
        public string? Currency { get; init; }
        public string? BillCountry { get; init; }
        public string? BillState { get; init; }
        public string? BillCity { get; init; }
        public string? BillAddress1 { get; init; }
        public string? BillAddress2 { get; init; }
        public string? BillPostCode { get; init; }
        public string? Phone { get; init; }
        public string? Mobile { get; init; }
        public string? Fax { get; init; }
        public string? TollFree { get; init; }
        public string? Portal { get; init; }
        public string? CorporateIdNumber { get; init; }
        public string? VatIdNumber { get; init; }
    }
}
