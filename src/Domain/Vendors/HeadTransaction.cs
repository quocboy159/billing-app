namespace Domain.Vendors;

public sealed class HeadTransaction
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int SubHeadsId { get; set; }
    public string? AccountTaxId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Currency { get; set; }

    /// <summary>
    /// Builds the HeadTransaction row that anchors a newly created vendor.
    /// <c>SubHeadsId</c> is fixed to <see cref="VendorConstants.VendorSubHeadId"/> (= 2003)
    /// to match the SP's hard-coded insert.
    /// </summary>
    public static HeadTransaction ForNewVendor(
        int companyId,
        string vendorName,
        string? accountNumber = null,
        string? currency = null) =>
        new()
        {
            CompanyId = companyId,
            SubHeadsId = VendorConstants.VendorSubHeadId,
            Name = vendorName,
            AccountTaxId = accountNumber,
            Currency = currency
        };
}
