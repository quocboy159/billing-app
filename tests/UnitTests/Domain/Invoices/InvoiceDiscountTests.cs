using Domain.Invoices;

namespace UnitTests.Domain.Invoices;

public sealed class InvoiceDiscountTests
{
    [Fact]
    public void No_discounts_returns_quantity_times_price()
    {
        // Arrange
        const int noDiscount = 0;

        // Act
        decimal result = InvoiceDiscount.AfterDiscount(
            invoiceDiscountType: noDiscount, invoiceDiscount: 0m,
            transactionDiscountType: noDiscount, transactionDiscount: 0m,
            quantity: 2m, price: 50m);

        // Assert
        result.ShouldBe(100m);
    }

    [Fact]
    public void Amount_type_transaction_discount_is_subtracted_as_is()
    {
        // Act
        decimal result = InvoiceDiscount.AfterDiscount(
            invoiceDiscountType: 0, invoiceDiscount: 0m,
            transactionDiscountType: InvoiceDiscount.AmountType, transactionDiscount: 15m,
            quantity: 2m, price: 50m);

        // Assert
        result.ShouldBe(100m - 15m);
    }

    [Fact]
    public void Percentage_type_transaction_discount_is_applied_to_line_total()
    {
        // 10% off 2 * 50 = 100 -> 90
        decimal result = InvoiceDiscount.AfterDiscount(
            invoiceDiscountType: 0, invoiceDiscount: 0m,
            transactionDiscountType: InvoiceDiscount.PercentageType, transactionDiscount: 10m,
            quantity: 2m, price: 50m);

        result.ShouldBe(90m);
    }

    [Fact]
    public void Amount_type_invoice_discount_is_subtracted_as_is()
    {
        // Invoice-level $5 off, no transaction discount.
        decimal result = InvoiceDiscount.AfterDiscount(
            invoiceDiscountType: InvoiceDiscount.AmountType, invoiceDiscount: 5m,
            transactionDiscountType: 0, transactionDiscount: 0m,
            quantity: 2m, price: 50m);

        result.ShouldBe(95m);
    }

    [Fact]
    public void Percentage_type_invoice_discount_is_dropped_to_match_SP_bug()
    {
        // The SP's @BasePrice is declared as 0 and never assigned, so the percentage
        // invoice-discount branch always evaluates to 0. We mirror that for SP parity.
        decimal result = InvoiceDiscount.AfterDiscount(
            invoiceDiscountType: InvoiceDiscount.PercentageType, invoiceDiscount: 20m,
            transactionDiscountType: 0, transactionDiscount: 0m,
            quantity: 2m, price: 50m);

        result.ShouldBe(100m); // NOT 80m -- the percentage invoice-discount is silently dropped.
    }

    [Fact]
    public void Both_discounts_combine_subtractively()
    {
        // Item: 4 * 25 = 100. Tx 10% -> 10 off. Invoice $5 off (amount). Expected: 100 - 10 - 5 = 85.
        decimal result = InvoiceDiscount.AfterDiscount(
            invoiceDiscountType: InvoiceDiscount.AmountType, invoiceDiscount: 5m,
            transactionDiscountType: InvoiceDiscount.PercentageType, transactionDiscount: 10m,
            quantity: 4m, price: 25m);

        result.ShouldBe(85m);
    }

    [Fact]
    public void Unknown_discount_type_is_treated_as_zero()
    {
        decimal result = InvoiceDiscount.AfterDiscount(
            invoiceDiscountType: 999, invoiceDiscount: 50m,
            transactionDiscountType: 999, transactionDiscount: 50m,
            quantity: 2m, price: 50m);

        result.ShouldBe(100m);
    }

    [Theory]
    // Equivalence with the inlined LINQ form used in GetProfitLossReportQueryHandler.cs.
    [InlineData(0, 0, 0, 0, 2, 50, 100)]   // baseline
    [InlineData(0, 0, 1, 15, 2, 50, 85)]   // tx amount
    [InlineData(0, 0, 2, 10, 2, 50, 90)]   // tx percent
    [InlineData(1, 5, 0, 0, 2, 50, 95)]   // inv amount
    [InlineData(2, 20, 0, 0, 2, 50, 100)]  // inv percent -> dropped (SP bug)
    [InlineData(1, 5, 2, 10, 4, 25, 85)]   // both
    public void Inline_handler_math_matches_helper(
        int invType, decimal invDisc, int txType, decimal txDisc,
        decimal qty, decimal price, decimal expected)
    {
        // The exact expression used by the LINQ projections in the report handler.
        decimal inline = (qty * price)
                         - (txType == 1
                             ? txDisc
                             : txType == 2
                                 ? (qty * price) * txDisc / 100m
                                 : 0m)
                         - (invType == 1 ? invDisc : 0m);

        decimal helper = InvoiceDiscount.AfterDiscount(invType, invDisc, txType, txDisc, qty, price);

        inline.ShouldBe(expected);
        helper.ShouldBe(expected);
        inline.ShouldBe(helper);
    }
}
