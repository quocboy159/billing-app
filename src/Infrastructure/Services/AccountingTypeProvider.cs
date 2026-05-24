using Application.Abstractions.Services;
using Domain.Companies;

namespace Infrastructure.Services;

/// <summary>
/// Stub implementation -- the original SP calls <c>dbo.AccountingTypeGet(@CompanyId)</c>.
/// Swap to a real lookup once the source table is available.
/// </summary>
internal sealed class AccountingTypeProvider : IAccountingTypeProvider
{
    public Task<AccountingType> GetAsync(int companyId, CancellationToken cancellationToken = default) =>
        Task.FromResult(AccountingType.Accrual);
}
