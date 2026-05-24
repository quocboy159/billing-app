namespace Domain.Banks;

public sealed class BankSubTransaction
{
    public int Id { get; set; }
    public int BankTransactionId { get; set; }
    public int TransactionHeadId { get; set; }
    public decimal Amount { get; set; }
    public BankTransactionType TransactionType { get; set; }
}
