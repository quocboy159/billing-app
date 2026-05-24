using SharedKernel;

namespace Domain.Bills;

public sealed record BillCreatedDomainEvent(Guid BillGuid, int CompanyId, string InvoiceNo) : IDomainEvent;
