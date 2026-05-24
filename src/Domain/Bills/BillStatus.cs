namespace Domain.Bills;

public enum BillStatus
{
    Draft = 0,
    Approved = 100,
    PartiallySettled = 150,
    Settled = 200,
    Deleted = 500
}
