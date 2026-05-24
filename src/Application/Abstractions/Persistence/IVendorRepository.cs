using Domain.Vendors;

namespace Application.Abstractions.Persistence;

public interface IVendorRepository
{
    Task<bool> ExistsByNameAsync(int companyId, string vendorName, CancellationToken cancellationToken = default);

    void Add(Vendor vendor);
}
