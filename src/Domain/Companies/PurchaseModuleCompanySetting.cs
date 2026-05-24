namespace Domain.Companies;

public sealed class PurchaseModuleCompanySetting
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string? BillNoPrefix { get; set; }
    public string? BillNoSuffix { get; set; }
}
