namespace Domain.Vendors;

/// <summary>
/// Value object capturing the optional descriptive fields of a Vendor. Used as a parameter
/// object to keep the <see cref="Vendor.CreateForCompany"/> factory ergonomic.
/// </summary>
public sealed record VendorProfile(
    string VendorName,
    string? AccountNumber = null,
    string? Currency = null,
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    string? BillCountry = null,
    string? BillState = null,
    string? BillCity = null,
    string? BillAddress1 = null,
    string? BillAddress2 = null,
    string? BillPostCode = null,
    string? Phone = null,
    string? Mobile = null,
    string? Fax = null,
    string? TollFree = null,
    string? Portal = null,
    string? TaxId = null,
    string? ContactPerson = null,
    string? CorporateIdNumber = null,
    string? VatIdNumber = null);
