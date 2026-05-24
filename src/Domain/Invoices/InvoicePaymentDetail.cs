namespace Domain.Invoices;

public sealed class InvoicePaymentDetail
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public decimal? Amount { get; set; }
    public int? BankSubTransactionId { get; set; }
}
