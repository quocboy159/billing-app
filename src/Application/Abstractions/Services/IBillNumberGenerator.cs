namespace Application.Abstractions.Services;

public sealed record BillNumber(int Counter, string InvoiceNo);

public interface IBillNumberGenerator
{
    Task<BillNumber> NextAsync(int companyId, CancellationToken cancellationToken = default);
}
