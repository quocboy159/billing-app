using Application.Abstractions.Persistence;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal sealed class CompanyRepository(ApplicationDbContext context) : ICompanyRepository
{
    public Task<string?> GetBusinessCurrencyAsync(int companyId, CancellationToken cancellationToken = default) =>
        context.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.BusinessCurrency)
            .SingleOrDefaultAsync(cancellationToken);
}
