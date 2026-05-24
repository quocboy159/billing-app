namespace Domain.Banks;

public sealed class BankTransaction
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public DateTime TransactionDate { get; set; }
    public BankTransactionType TransactionType { get; set; }
    public int? ExpenseId { get; set; }
}
