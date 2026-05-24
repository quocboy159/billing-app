namespace Application.Bills.Create;

public sealed class CreateBillResponse
{
    public int Id { get; init; }
    public Guid Guid { get; init; }
    public string InvoiceNo { get; init; } = string.Empty;
}
