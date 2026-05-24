using Application.Abstractions.Persistence;
using Domain.Vendors;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal sealed class HeadTransactionRepository(ApplicationDbContext context) : IHeadTransactionRepository
{
    public Task<bool> BelongsToCompanyAsync(int headTransactionId, int companyId, CancellationToken cancellationToken = default) =>
        context.HeadTransactions
            .AsNoTracking()
            .AnyAsync(h => h.Id == headTransactionId && h.CompanyId == companyId, cancellationToken);

    public void Add(HeadTransaction headTransaction) => context.HeadTransactions.Add(headTransaction);
}
