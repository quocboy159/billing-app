using Application.Abstractions.Messaging;

namespace Application.Bills.Create;

public sealed class CreateBillCommand : ICommand<CreateBillResponse>
{
    public BillHeaderInput Bill { get; set; } = new();
    public List<BillTransactionInput> Transactions { get; set; } = [];
    public List<BillTransactionTaxInput> Taxes { get; set; } = [];
    public VendorInput? Vendor { get; set; }
    public string? UserName { get; set; }
}

public sealed class BillHeaderInput
{
    public int CompanyId { get; set; }
    public int HeadTransactionVendorId { get; set; }
    public string? Currency { get; set; }
    public string? PoNumber { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal Price { get; set; }
    public DateTime BillAt { get; set; }
    public DateTime DueAt { get; set; }
    public string? Notes { get; set; }
}

public sealed class BillTransactionInput
{
    public int Counter { get; set; }
    public int ProductId { get; set; }
    public string? Description { get; set; }
    public int ProductTransactionHeadId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}

public sealed class BillTransactionTaxInput
{
    public int CounterTranId { get; set; }
    public int TransactionHeadTaxId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}

public sealed class VendorInput
{
    public string VendorName { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string? Currency { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? BillCountry { get; set; }
    public string? BillState { get; set; }
    public string? BillCity { get; set; }
    public string? BillAddress1 { get; set; }
    public string? BillAddress2 { get; set; }
    public string? BillPostCode { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
    public string? TollFree { get; set; }
    public string? Portal { get; set; }
    public string? TaxId { get; set; }
    public string? ContactPerson { get; set; }
    public string? CorporateIdNumber { get; set; }
    public string? VatIdNumber { get; set; }
}
