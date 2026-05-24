namespace Domain.Expenses;

public sealed class Expense
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public DateTime ExpenseDate { get; set; }
}
