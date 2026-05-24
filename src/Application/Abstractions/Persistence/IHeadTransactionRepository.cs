using Domain.Vendors;

namespace Application.Abstractions.Persistence;

public interface IHeadTransactionRepository
{
    Task<bool> BelongsToCompanyAsync(int headTransactionId, int companyId, CancellationToken cancellationToken = default);

    void Add(HeadTransaction headTransaction);
}
