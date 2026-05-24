using Domain.Bills;

namespace Application.Bills.GetForExport;

public sealed class BillExportRowResponse
{
    public int Id { get; init; }
    public Guid Guid { get; init; }
    public int HeadTransactionVendorId { get; init; }
    public string? VendorName { get; init; }
    public BillStatus Status { get; init; }
    public string InvoiceNo { get; init; } = string.Empty;
    public DateTime BillAt { get; init; }
    public DateTime DueAt { get; init; }
    public decimal Price { get; init; }
    public decimal DueAmount { get; init; }
    public string? Currency { get; init; }
    public string? Notes { get; init; }
    public decimal TotalTax { get; init; }
    public decimal SubTotal { get; init; }
}
