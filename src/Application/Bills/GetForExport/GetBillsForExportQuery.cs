using Application.Abstractions.Messaging;
using Domain.Bills;

namespace Application.Bills.GetForExport;

public sealed class GetBillsForExportQuery : IQuery<List<BillExportRowResponse>>
{
    public int CompanyId { get; set; }
    public int? VendorId { get; set; }
    public BillExportStatusFilter? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? InvoiceNo { get; set; }
    public string? FilterKeyword { get; set; }
    public int PageNumber { get; set; } = 1;
    public int RecordsPerPage { get; set; } = 20;
    /// <summary>
    /// keep deleted bills visible in the trash for N days
    /// It's a retention window in days for the "show me my deleted bills" view.
    /// The parameter only matters when the caller is filtering by Status = 2000 (the synthetic "Deleted" filter status); for any other status it's ignored.
    /// </summary>
    public int DeletedInvoiceDisplayFor { get; set; }
}

public sealed class BillExportStatusFilter
{
    public BillStatus? Status { get; init; }
    public BillFilterStatus? FilterStatus { get; init; }

    public static BillExportStatusFilter FromInt(int raw) =>
        Enum.IsDefined(typeof(BillFilterStatus), raw)
            ? new BillExportStatusFilter { FilterStatus = (BillFilterStatus)raw }
            : new BillExportStatusFilter { Status = (BillStatus)raw };
}
