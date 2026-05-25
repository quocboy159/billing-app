namespace Domain.Invoices;

/// <summary>
/// Canonical port of <c>dbo.GetTransactionAmountAfterDiscount</c> (and its V2 sibling).
///
/// Single source of truth for the discount formula used by Invoices / CreditNotes in the
/// P&amp;L report. The LINQ projections in <c>GetProfitLossReportQueryHandler</c> inline the
/// same math literally -- EF Core 10 cannot translate a method call to SQL, so the math
/// must appear in the lambda body -- but they reference this class via comment so the
/// formula has exactly one documented authority and is unit-testable.
///
/// Formula (with @ExchangeRate = 1, ignoring DECIMAL(24,10) rounding):
///   ItemPrice                 = Qty * Price
///   TransactionDiscountResult = TxType=Amount ? TxDisc
///                             : TxType=Percentage ? (Qty * Price) * TxDisc / 100
///                             : 0
///   InvoiceDiscountResult     = InvType=Amount ? InvDisc
///                             : InvType=Percentage ? 0  (SP bug -- @BasePrice never assigned)
///                             : 0
///   Result = ItemPrice - InvoiceDiscountResult - TransactionDiscountResult
/// </summary>
public static class InvoiceDiscount
{
    public const int AmountType = 1;
    public const int PercentageType = 2;

    public static decimal AfterDiscount(
        int invoiceDiscountType,
        decimal invoiceDiscount,
        int transactionDiscountType,
        decimal transactionDiscount,
        decimal quantity,
        decimal price)
    {
        decimal itemPrice = quantity * price;

        decimal transactionDiscountResult = transactionDiscountType switch
        {
            AmountType => transactionDiscount,
            PercentageType => itemPrice * transactionDiscount / 100m,
            _ => 0m
        };

        // SP collapses Type=Percentage to 0 because @BasePrice is declared as 0 and never set.
        // We preserve that behaviour so the C# value matches the SP output bit-for-bit
        // (modulo Decimal vs decimal(24,10) precision).
        decimal invoiceDiscountResult = invoiceDiscountType == AmountType
            ? invoiceDiscount
            : 0m;

        return itemPrice - invoiceDiscountResult - transactionDiscountResult;
    }
}
