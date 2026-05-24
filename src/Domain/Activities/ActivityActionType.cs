namespace Domain.Activities;

public enum ActivityActionType
{
    BillCreated = 401,
    BillUpdated = 402,
    BillApproved = 403,
    BillDeleteRollback = 405,
    BillArchiveRollback = 407
}
