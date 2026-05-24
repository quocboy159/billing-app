using Application.Abstractions.Data;
using Application.Abstractions.Services;
using Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

internal sealed class BillNumberGenerator(IApplicationDbContext context) : IBillNumberGenerator
{
    public async Task<BillNumber> NextAsync(int companyId, CancellationToken cancellationToken = default)
    {
        PurchaseCounter? counter = await context.PurchaseCounters
            .SingleOrDefaultAsync(c => c.CompanyId == companyId, cancellationToken);

        int next = (counter?.BillNo ?? 0) + 1;

        PurchaseModuleCompanySetting? settings = await context.PurchaseModuleCompanySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);

        string invoiceNo = ((settings?.BillNoPrefix ?? string.Empty) + next + (settings?.BillNoSuffix ?? string.Empty))
            .ToUpperInvariant();

        if (counter is null)
        {
            context.PurchaseCounters.Add(new PurchaseCounter { CompanyId = companyId, BillNo = next });
        }
        else
        {
            counter.BillNo = next;
        }

        return new BillNumber(next, invoiceNo);
    }
}
