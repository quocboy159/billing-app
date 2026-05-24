using Application.Abstractions.Messaging;

namespace Application.Bills.GetByGuid;

public sealed class GetBillByGuidQuery : IQuery<BillDetailResponse>
{
    public Guid Guid { get; set; }
    public int CompanyId { get; set; }
}
