namespace Domain.Vendors;

public sealed class Vendor
{
    public int Id { get; set; }
    public int HeadTransactionId { get; set; }
    public int CompanyId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string? Email { get; set; }
    public string? Currency { get; set; }
    public string? BillCountry { get; set; }
    public string? BillState { get; set; }
    public string? BillCity { get; set; }
    public string? BillAddress1 { get; set; }
    public string? BillAddress2 { get; set; }
    public string? BillPostCode { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
    public string? TollFree { get; set; }
    public string? Portal { get; set; }
    public bool IsActive { get; set; }
    public DateTime Created { get; set; }
    public string? TaxId { get; set; }
    public string? ContactPerson { get; set; }
    public string? CorporateIdNumber { get; set; }
    public string? VatIdNumber { get; set; }

    /// <summary>
    /// Factory for the "create vendor on the fly" branch of <c>BillsAdd</c>.
    /// Mirrors the SP's INSERT INTO Vendors block (IsActive=1, Created=now), and links the
    /// vendor to the HeadTransaction the caller has just created.
    /// </summary>
    public static Vendor CreateForCompany(int companyId, int headTransactionId, VendorProfile profile, DateTime utcNow) =>
        new()
        {
            CompanyId = companyId,
            HeadTransactionId = headTransactionId,
            VendorName = profile.VendorName,
            AccountNumber = profile.AccountNumber,
            Currency = profile.Currency,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Email = profile.Email,
            BillCountry = profile.BillCountry,
            BillState = profile.BillState,
            BillCity = profile.BillCity,
            BillAddress1 = profile.BillAddress1,
            BillAddress2 = profile.BillAddress2,
            BillPostCode = profile.BillPostCode,
            Phone = profile.Phone,
            Mobile = profile.Mobile,
            Fax = profile.Fax,
            TollFree = profile.TollFree,
            Portal = profile.Portal,
            TaxId = profile.TaxId,
            ContactPerson = profile.ContactPerson,
            CorporateIdNumber = profile.CorporateIdNumber,
            VatIdNumber = profile.VatIdNumber,
            IsActive = true,
            Created = utcNow
        };
}
