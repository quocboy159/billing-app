namespace Domain.Activities;

public sealed class Activity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int TableTransactionId { get; set; }
    public string? TableTransactionValue { get; set; }
    public ActivityTableType TableEnumType { get; set; }
    public ActivityActionType ActionType { get; set; }
    public string? TransactionHeadName { get; set; }
    public string? Remarks { get; set; }
    public string? UserName { get; set; }
    public DateTime Created { get; set; }
}
