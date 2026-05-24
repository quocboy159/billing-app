namespace Domain.Expenses;

public sealed class ExpenseTransaction
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public int CompanyId { get; set; }
    public int TransactionHeadId { get; set; }
    public decimal Amount { get; set; }
    public decimal InvoiceAmount { get; set; }
}
