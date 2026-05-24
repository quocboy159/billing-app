SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER PROCEDURE [dbo].[ReportProfitLossGet] 
 @CompanyId as INT
,@StartDate1 as DATETIME
,@EndDate1 as DATETIME
,@StartDate2 as DATETIME=NULL
,@EndDate2 as DATETIME=NULL
,@ReportType AS INT=NULL
,@IsRetain AS bit='0'
AS
BEGIN
	SET NOCOUNT ON;
DECLARE @APPROVED AS INT =100, @PARTIAL_SETTLED AS INT =150, @SETTLED AS INT =200
	DECLARE @INVOICE AS INT=1,@BILL AS INT =2
	DECLARE @AccountType AS INT=1;
	SET @AccountType=dbo.AccountingTypeGet(@CompanyId)
				
				SELECT @INVOICE AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,
                (cast([dbo].[GetTransactionAmountAfterDiscount](I.DiscountType,I.Discount,T.DiscountType,T.Discount,T.Quantity,T.Price) as decimal(24,10))*(case @AccountType when 2 then 1 else cast((1.0 *ipd.Amount/I.Amount) as decimal(24,10)) end)) as ProductPrice,
				ISNULL(PnL.OrderId,100) OrderId
			    FROM Invoices I
				LEFT JOIN InvoiceTransactions t ON I.Id=t.InvoiceId
				LEFT JOIN ProductServices p ON t.ProductId=p.Id
				LEFT JOIN HeadTransactions h ON t.ProductTransactionHeadId=h.Id
				INNER JOIN HeadSubs s ON s.Id=h.SubHeadsId
				LEFT JOIN InvoicePaymentDetails ipd  ON ipd.InvoiceId = I.Id
				LEFT JOIN BankSubTransactions bst on bst.Id = ipd.BankSubTransactionId
				LEFT JOIN BankTransactions bt on bst.BankTransactionId = bt.Id
				RIGHT JOIN tblProfitNLoss PnL ON s.Id=PnL.SubHeadId
				WHERE  
				I.CompanyId=@CompanyId 
				AND ((
					@AccountType = 2
					AND I.Status IN (@APPROVED, @PARTIAL_SETTLED, @SETTLED)
					AND (
						(
							@IsRetain = '1'
							OR InvoiceDate >= @StartDate1
						)
						AND InvoiceDate <= @EndDate1)
					)
				OR
				(
					@AccountType = 1
					AND I.Status IN (@SETTLED, @PARTIAL_SETTLED)
					AND bt.TransactionDate IS NOT NULL
					AND bt.TransactionDate >= @StartDate1 AND bt.TransactionDate <= @EndDate1
				)
				)
			    UNION ALL
			    SELECT @INVOICE AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,
                -[dbo].[GetTransactionAmountAfterDiscount](I.DiscountType,I.Discount,T.DiscountType,T.Discount,T.Quantity,T.Price) as ProductPrice,
				ISNULL(PnL.OrderId,100) OrderId
			    FROM CreditNotes I INNER JOIN Invoices II ON I.InvoiceId=II.Id
				LEFT JOIN CreditNoteTransactions t ON I.Id=t.CreditNoteId
				LEFT JOIN ProductServices p ON t.ProductId=p.Id
				LEFT JOIN HeadTransactions h ON P.HeadTransactionId=h.Id
				INNER JOIN HeadSubs s ON s.Id=h.SubHeadsId
				RIGHT JOIN tblProfitNLoss PnL ON s.Id=PnL.SubHeadId
				WHERE @AccountType=2 AND I.CompanyId=@CompanyId AND I.Status=@APPROVED AND II.Status IN (@APPROVED,@PARTIAL_SETTLED,@SETTLED) AND ((@IsRetain='1' OR CreditNoteDate>=@StartDate1) AND CreditNoteDate<=@EndDate1)
				UNION ALL
				SELECT @BILL AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,
                cast((t.Price * t.Quantity) as decimal(24,10))*(case @AccountType when 2 then 1 else cast((1.0 * bpd.Amount/I.Price) as decimal(24,10)) end) as ProductPrice,
				ISNULL(PnL.OrderId,100) OrderId
			    FROM Bills I
				LEFT JOIN BillTransactions t ON I.Id=t.BillId
				LEFT JOIN ProductServices p ON t.ProductId=p.Id
				LEFT JOIN HeadTransactions h ON t.ProductTransactionHeadId=h.Id
				LEFT JOIN BillPaymentDetails bpd  ON bpd.BillId = I.Id
				LEFT JOIN BankSubTransactions bst on bst.Id = bpd.BankSubTransactionId
				LEFT JOIN BankTransactions bt on bst.BankTransactionId = bt.Id
				INNER JOIN HeadSubs s ON s.Id=h.SubHeadsId
				LEFT JOIN tblProfitNLoss PnL ON s.Id=PnL.SubHeadId
				WHERE I.CompanyId = @CompanyId
				and (
						(
							@AccountType = 2
							AND I.Status IN (@APPROVED, @PARTIAL_SETTLED, @SETTLED)
							AND (
									(
										@IsRetain = '1'
										OR BillAt >= @StartDate1
									)
									AND 
									BillAt <= @EndDate1
								)
						)
						or
						(
							@AccountType = 1
							AND I.Status IN (@SETTLED, @PARTIAL_SETTLED)
							AND bt.TransactionDate IS NOT NULL
							AND bt.TransactionDate >= @StartDate1 AND bt.TransactionDate <= @EndDate1
						)
					)
				UNION ALL
				SELECT (CASE WHEN s.HeadId=5000 THEN @BILL ELSE @INVOICE END) AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,
				(CASE 
				WHEN s.HeadId=5000 AND (I.TransactionType=1 OR (I.TransactionType=3 AND T.TransactionType=1)) THEN -T.Amount  
				WHEN s.HeadId=5000  AND (I.TransactionType=2 OR (I.TransactionType=3 AND T.TransactionType=2)) THEN T.Amount
				WHEN s.HeadId=4000 AND (I.TransactionType=2 OR (I.TransactionType=3 AND T.TransactionType=2)) THEN -T.Amount
				WHEN s.HeadId=4000 AND (I.TransactionType=1 OR (I.TransactionType=3 AND T.TransactionType=1)) THEN T.Amount
				END) as ProductPrice,ISNULL(PnL.OrderId,100) OrderId
			    FROM BankTransactions I	INNER JOIN BankSubTransactions t ON I.Id=t.BankTransactionId
				INNER JOIN HeadTransactions h ON t.TransactionHeadId=h.Id
				INNER JOIN HeadSubs s ON s.Id=h.SubHeadsId
				LEFT JOIN tblProfitNLoss PnL ON s.Id=PnL.SubHeadId
				WHERE I.CompanyId=@CompanyId AND  s.HeadId IN (5000,4000)  AND ((@IsRetain='1' OR I.TransactionDate>=@StartDate1) AND I.TransactionDate<=@EndDate1)
				AND I.ExpenseId IS NULL
			
	
		        UNION ALL
				SELECT (CASE WHEN s.HeadId=5000 THEN @BILL ELSE @INVOICE END) AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,				
				T.Amount as ProductPrice,100 AS OrderId
				FROM ExpenseTransactions T
				INNER JOIN  Expenses E ON E.Id=T.ExpenseId				
				INNER JOIN HeadTransactions h ON  h.Id = T.TransactionHeadId
				INNER JOIN dbo.HeadSubs s ON s.Id = h.SubHeadsId
				
				WHERE T.CompanyId = @CompanyId AND s.HeadId IN (5000)  AND ((@IsRetain='1' OR E.ExpenseDate>=@StartDate1) AND E.ExpenseDate<=@EndDate1)
				
				UNION ALL
				SELECT (CASE WHEN s.HeadId=5000 THEN @BILL ELSE @INVOICE END) AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,				
				-T.InvoiceAmount as ProductPrice,100 AS OrderId
				FROM ExpenseTransactions T
				INNER JOIN  Expenses E ON E.Id=T.ExpenseId
				INNER JOIN HeadTransactions h ON  h.Id = T.TransactionHeadId
				INNER JOIN dbo.HeadSubs s ON s.Id = h.SubHeadsId
				
				WHERE E.CompanyId = @CompanyId AND s.HeadId IN (5000) AND T.InvoiceAmount!=0 AND ((@IsRetain='1' OR E.ExpenseDate>=@StartDate1) AND E.ExpenseDate<=@EndDate1)
				
  

IF @StartDate2 IS NOT NULL AND @EndDate2 IS NOT NULL
BEGIN
				
				SELECT @INVOICE AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,
                (cast(ISNULL(
                    [dbo].[GetTransactionAmountAfterDiscount](
                        I.DiscountType,
                        I.Discount,
                        T.DiscountType,
                        T.Discount,
                        T.Quantity,
                        T.Price
                    ),
                    0
                ) as decimal(24,10))*(case @AccountType when 2 then 1 else cast((1.0 * ipd.Amount/I.Amount) as decimal(24,10)) end)) as ProductPrice,
				ISNULL(PnL.OrderId,100) OrderId
			    FROM Invoices I
				LEFT JOIN InvoiceTransactions t ON I.Id=t.InvoiceId
				LEFT JOIN ProductServices p ON t.ProductId=p.Id
				LEFT JOIN HeadTransactions h ON t.ProductTransactionHeadId=h.Id
				LEFT JOIN InvoicePaymentDetails ipd  ON ipd.InvoiceId = I.Id
				LEFT JOIN BankSubTransactions bst on bst.Id = ipd.BankSubTransactionId
				LEFT JOIN BankTransactions bt on bst.BankTransactionId = bt.Id
				INNER JOIN HeadSubs s ON s.Id=h.SubHeadsId
				LEFT JOIN tblProfitNLoss PnL ON s.Id=PnL.SubHeadId
				WHERE I.CompanyId = @CompanyId
				AND ((
						@AccountType = 2
						AND I.Status IN (@APPROVED, @PARTIAL_SETTLED, @SETTLED)
						AND InvoiceDate <= @EndDate2 AND InvoiceDate >= @StartDate2
					)
					OR
					(
						@AccountType = 1
						AND I.Status IN (@SETTLED, @PARTIAL_SETTLED)
						AND bt.TransactionDate IS NOT NULL
						AND bt.TransactionDate >= @StartDate2 AND bt.TransactionDate <= @EndDate2
					)
				)
				UNION ALL
				SELECT @INVOICE AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,
               -[dbo].[GetTransactionAmountAfterDiscount](I.DiscountType,I.Discount,T.DiscountType,T.Discount,T.Quantity,T.Price) as ProductPrice,ISNULL(PnL.OrderId,100) OrderId
			    FROM CreditNotes I INNER JOIN Invoices II ON I.InvoiceId=II.Id
				LEFT JOIN CreditNoteTransactions t ON I.Id=t.CreditNoteId
				LEFT JOIN ProductServices p ON t.ProductId=p.Id
				LEFT JOIN HeadTransactions h ON p.HeadTransactionId=h.Id
				INNER JOIN HeadSubs s ON s.Id=h.SubHeadsId
				LEFT JOIN tblProfitNLoss PnL ON s.Id=PnL.SubHeadId
				WHERE @AccountType=2 AND I.CompanyId=@CompanyId AND I.Status=@APPROVED  AND II.Status IN (@APPROVED,@PARTIAL_SETTLED,@SETTLED) AND (CreditNoteDate>=@StartDate2 AND CreditNoteDate<=@EndDate2)
				UNION ALL
				SELECT @BILL AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,
                (cast((t.Price * t.Quantity) as decimal(24,10))*(case @AccountType when 2 then 1 else cast((1.0 * bpd.Amount/I.Price) as decimal(24,10)) end)) as ProductPrice,
				ISNULL(PnL.OrderId,100) OrderId
			    FROM Bills I
				LEFT JOIN BillTransactions t ON I.Id=t.BillId
				LEFT JOIN ProductServices p ON t.ProductId=p.Id
				LEFT JOIN HeadTransactions h ON t.ProductTransactionHeadId=h.Id
				LEFT JOIN BillPaymentDetails bpd  ON bpd.BillId = I.Id
				LEFT JOIN BankSubTransactions bst on bst.Id = bpd.BankSubTransactionId
				LEFT JOIN BankTransactions bt on bst.BankTransactionId = bt.Id
				INNER JOIN HeadSubs s ON s.Id=h.SubHeadsId
				LEFT JOIN tblProfitNLoss PnL ON s.Id=PnL.SubHeadId
				 WHERE I.CompanyId = @CompanyId
			and (
					(
						@AccountType = 2
						AND I.Status IN (@APPROVED, @PARTIAL_SETTLED, @SETTLED)
						AND BillAt <= @EndDate2 AND BillAt >= @StartDate2
					)
					or
					(
						@AccountType = 1
						AND I.Status IN (@SETTLED, @PARTIAL_SETTLED)
						AND bt.TransactionDate IS NOT NULL
						AND bt.TransactionDate >= @StartDate2 AND bt.TransactionDate <= @EndDate2
					)
				)
				UNION ALL
				SELECT (CASE WHEN s.HeadId=5000 THEN @BILL ELSE @INVOICE END) AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,
				(CASE 
				WHEN s.HeadId=5000 AND (I.TransactionType=1 OR (I.TransactionType=3 AND T.TransactionType=1)) THEN -T.Amount  
				WHEN s.HeadId=5000  AND (I.TransactionType=2 OR (I.TransactionType=3 AND T.TransactionType=2)) THEN T.Amount
				WHEN s.HeadId=4000 AND (I.TransactionType=2 OR (I.TransactionType=3 AND T.TransactionType=2)) THEN -T.Amount
				WHEN s.HeadId=4000 AND (I.TransactionType=1 OR (I.TransactionType=3 AND T.TransactionType=1)) THEN T.Amount
				END) as ProductPrice,ISNULL(PnL.OrderId,100) OrderId
			    FROM BankTransactions I	INNER JOIN BankSubTransactions t ON I.Id=t.BankTransactionId
				INNER JOIN HeadTransactions h ON t.TransactionHeadId=h.Id
				INNER JOIN HeadSubs s ON s.Id=h.SubHeadsId
				LEFT JOIN tblProfitNLoss PnL ON s.Id=PnL.SubHeadId
				WHERE I.CompanyId=@CompanyId AND  s.HeadId IN (5000,4000)  AND (I.TransactionDate>=@StartDate2 AND I.TransactionDate<=@EndDate2)
				AND I.ExpenseId IS NULL
			

            
		        UNION ALL
				SELECT (CASE WHEN s.HeadId=5000 THEN @BILL ELSE @INVOICE END) AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,				
				T.Amount as ProductPrice,100 AS OrderId
				FROM ExpenseTransactions T
				INNER JOIN  Expenses E ON E.Id=T.ExpenseId				
				INNER JOIN HeadTransactions h ON  h.Id = T.TransactionHeadId
				INNER JOIN dbo.HeadSubs s ON s.Id = h.SubHeadsId
				
				WHERE T.CompanyId = @CompanyId AND s.HeadId IN (5000)  AND (E.ExpenseDate>=@StartDate2 AND E.ExpenseDate<=@EndDate2)
				
				UNION ALL
				SELECT (CASE WHEN s.HeadId=5000 THEN @BILL ELSE @INVOICE END) AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,				
				-T.InvoiceAmount as ProductPrice,100 AS OrderId
				FROM ExpenseTransactions T
				INNER JOIN  Expenses E ON E.Id=T.ExpenseId
				
				INNER JOIN HeadTransactions h ON  h.Id = T.TransactionHeadId
				INNER JOIN dbo.HeadSubs s ON s.Id = h.SubHeadsId
				
				WHERE E.CompanyId = @CompanyId AND s.HeadId IN (5000) AND T.InvoiceAmount!=0 AND (E.ExpenseDate>=@StartDate2 AND E.ExpenseDate<=@EndDate2) 
				
               
END
END;