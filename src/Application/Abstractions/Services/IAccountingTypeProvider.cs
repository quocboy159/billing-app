using Domain.Companies;

namespace Application.Abstractions.Services;

public interface IAccountingTypeProvider
{
    Task<AccountingType> GetAsync(int companyId, CancellationToken cancellationToken = default);
}
