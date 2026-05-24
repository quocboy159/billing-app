namespace Domain.Bills;

public sealed class BillPaymentDetail
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public decimal? Amount { get; set; }
}
