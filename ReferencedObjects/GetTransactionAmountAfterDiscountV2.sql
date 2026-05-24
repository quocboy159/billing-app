CREATE FUNCTION [dbo].[GetTransactionAmountAfterDiscountV2]
(
    @InvoiceDiscountType INT,
    @InvoiceDiscount DECIMAL(18, 4),
    @TransactionDiscountType INT,
    @TransactionDiscount DECIMAL(18, 4),
    @Quantity DECIMAL(18, 4),
    @Price DECIMAL(18, 10),
	@CustExchangeRate decimal(18,9)=1.0,
	@InvoiceItemFraction INT=8
	
)
RETURNS DECIMAL(18, 10)
AS
BEGIN
		DECLARE @DiscountTypeAmount INT = 1,
		@DiscountTypePercentage INT = 2;
		DECLARE @BasePrice DECIMAL(18, 10) = 0
		DECLARE @ItemPrice DECIMAL(18, 10) = 0

		DECLARE @TransactionDiscountResult DECIMAL(18, 10) = 0
		DECLARE @InvoiceDiscountResult DECIMAL(18, 10) = 0

		set @ItemPrice = ROUND(@Quantity * @Price / @CustExchangeRate, @InvoiceItemFraction)

		--Discount calculated at item level
		 SET @TransactionDiscountResult = CASE 
			WHEN @TransactionDiscountType = @DiscountTypeAmount 
			THEN ROUND((@TransactionDiscount / @CustExchangeRate), @InvoiceItemFraction)
			ELSE CASE 
				WHEN @TransactionDiscountType = @DiscountTypePercentage 
				THEN ROUND(((((@Quantity * @Price) * @TransactionDiscount) / 100) / @CustExchangeRate), @InvoiceItemFraction) 
				ELSE 0 
				END 
			END

		--Discount calculated at invoice level
		 SET @InvoiceDiscountResult = CASE 
			WHEN @InvoiceDiscountType = @DiscountTypeAmount 
			THEN ROUND((@InvoiceDiscount / @CustExchangeRate), @InvoiceItemFraction)
			ELSE CASE 
				WHEN @InvoiceDiscountType = @DiscountTypePercentage 
				THEN ROUND((((@BasePrice * @InvoiceDiscount) / 100) / @CustExchangeRate), @InvoiceItemFraction) 
				ELSE 0 
				END 
			END

		 RETURN CAST((@ItemPrice - @InvoiceDiscountResult - @TransactionDiscountResult) AS DECIMAL(18,10));
END