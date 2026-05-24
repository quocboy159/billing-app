using Application.Abstractions.Persistence;
using Domain.Vendors;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal sealed class VendorRepository(ApplicationDbContext context) : IVendorRepository
{
    public Task<bool> ExistsByNameAsync(int companyId, string vendorName, CancellationToken cancellationToken = default) =>
        context.Vendors
            .AsNoTracking()
            .AnyAsync(v => v.CompanyId == companyId && v.VendorName == vendorName, cancellationToken);

    public void Add(Vendor vendor) => context.Vendors.Add(vendor);
}
