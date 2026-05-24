SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
/* =============================================================================================
   EF Core port :  src/Application/Reports/ProfitLoss/GetProfitLossReportQueryHandler.cs
   Endpoint     :  POST /reports/profit-loss  (src/Web.Api/Endpoints/Reports/GetProfitLoss.cs)
   Query DTO    :  GetProfitLossReportQuery -> ProfitLossReportResponse { Period1, Period2 }

   Shape:
     One row per (Source, SubHead, TransactionHead) bucket. The SP unions 6 buckets per period,
     optionally repeating for a 2nd comparison period. EF mirrors this with 6
     IQueryable<ProfitLossRow> Concat'ed together (translates to UNION ALL), invoked once
     per period.

   The 6 buckets, with their EF Core counterparts in GetProfitLossReportQueryHandler.cs:
     1. Invoices                                -> Invoices(...)            (+, accrual or cash)
     2. CreditNotes against approved invoices   -> CreditNotes(...)         (-, accrual only)
     3. Bills                                   -> Bills(...)               (+, accrual or cash)
     4. Direct BankTransactions (HeadId 4000/5000, ExpenseId IS NULL)
                                                -> BankDirect(...)         (sign depends on Type x HeadId)
     5. ExpenseTransactions on HeadId=5000      -> ExpensesPositive(...)   (+)
     6. ExpenseTransactions.InvoiceAmount       -> ExpensesNegative(...)   (-, reimbursement)

   Cross-cutting:
     - @AccountType <- dbo.AccountingTypeGet(@CompanyId)
         EF: IAccountingTypeProvider.GetAsync
         Accrual (=2): full amount, filter by InvoiceDate / BillAt
         Cash    (=1): amount scaled by (paid/total), filter by BankTransactions.TransactionDate
     - @IsRetain = 1 -> ignore StartDate lower bound (retained-earnings calculation).
       Only applies to Period 1; Period 2 ignores IsRetain.
     - dbo.GetTransactionAmountAfterDiscount(...) is INLINED into the LINQ projections because
       EF Core can't translate a private method call. The math:
           base = Qty * Price
           tx   = Type=1 ? TxDisc : Type=2 ? base*TxDisc/100 : 0
           inv  = Type=1 ? InvDisc : 0    -- mirrors SP's BasePrice=0 bug for percentage invoice-disc
           amt  = base - tx - inv
     - tblProfitNLoss is left-joined (the SP uses RIGHT JOIN which silently drops rows without
       a matching category -- almost certainly a bug; we LEFT JOIN with OrderId default 100).
   ============================================================================================= */
ALTER PROCEDURE [dbo].[ReportProfitLossGet]
 @CompanyId as INT
,@StartDate1 as DATETIME
,@EndDate1 as DATETIME
,@StartDate2 as DATETIME=NULL
,@EndDate2 as DATETIME=NULL
,@ReportType AS INT=NULL                       -- declared but unused by the SP; not modelled in EF
,@IsRetain AS bit='0'
AS
BEGIN
	SET NOCOUNT ON;
-- Status / Type sentinels. EF mirrors these via Domain.Invoices.InvoiceStatus,
-- Domain.Bills.BillStatus, and the ProfitLossRowType enum.
DECLARE @APPROVED AS INT =100, @PARTIAL_SETTLED AS INT =150, @SETTLED AS INT =200
	DECLARE @INVOICE AS INT=1,@BILL AS INT =2
	DECLARE @AccountType AS INT=1;
	-- EF equivalent: AccountingType accountType = await accountingTypes.GetAsync(companyId, ct);
	SET @AccountType=dbo.AccountingTypeGet(@CompanyId)
				
				-- ================== Period 1 ==================
				-- Bucket 1: Invoices (revenue, positive).
				-- EF equivalent: GetProfitLossReportQueryHandler.Invoices(...)
				-- Accrual branch: full amount, InvoiceDate window.
				-- Cash branch:    joins ipd -> BankSubTransactions -> BankTransactions,
				--                 scales by ipd.Amount / inv.Amount, filters by bt.TransactionDate.
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
			    -- Bucket 2: CreditNotes against approved invoices (revenue offset, negative).
			    -- EF equivalent: GetProfitLossReportQueryHandler.CreditNotes(...)
			    -- Guard: only on Accrual basis (the SP's WHERE clause hard-codes @AccountType=2).
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
				-- Bucket 3: Bills (cost, positive). Same cash/accrual logic as Invoices, no discount math.
				-- EF equivalent: GetProfitLossReportQueryHandler.Bills(...)
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
				-- Bucket 4: Direct BankTransactions on HeadId IN (5000 expense, 4000 income),
				-- excluding rows tied to an Expense (those are covered by bucket 5/6).
				-- EF equivalent: GetProfitLossReportQueryHandler.BankDirect(...)
				-- Sign matrix:
				--   Expense head + Debit-like  -> -Amount  (refund / undo expense)
				--   Expense head + Credit-like -> +Amount  (paid expense)
				--   Income head  + Credit-like -> -Amount  (refunded revenue)
				--   Income head  + Debit-like  -> +Amount  (revenue received)
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
				-- Bucket 5: ExpenseTransactions on HeadId=5000 (expense, positive).
				-- EF equivalent: GetProfitLossReportQueryHandler.ExpensesPositive(...)
				SELECT (CASE WHEN s.HeadId=5000 THEN @BILL ELSE @INVOICE END) AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,
				T.Amount as ProductPrice,100 AS OrderId
				FROM ExpenseTransactions T
				INNER JOIN  Expenses E ON E.Id=T.ExpenseId
				INNER JOIN HeadTransactions h ON  h.Id = T.TransactionHeadId
				INNER JOIN dbo.HeadSubs s ON s.Id = h.SubHeadsId

				WHERE T.CompanyId = @CompanyId AND s.HeadId IN (5000)  AND ((@IsRetain='1' OR E.ExpenseDate>=@StartDate1) AND E.ExpenseDate<=@EndDate1)

				UNION ALL
				-- Bucket 6: ExpenseTransactions.InvoiceAmount (reimbursement, negative).
				-- EF equivalent: GetProfitLossReportQueryHandler.ExpensesNegative(...)
				SELECT (CASE WHEN s.HeadId=5000 THEN @BILL ELSE @INVOICE END) AS Type,s.HeadId AS HeadId, h.SubHeadsId AS SubHeadId,s.Name AS SubHeadName,h.Id AS TransactionId,h.Name AS TransactionName,
				-T.InvoiceAmount as ProductPrice,100 AS OrderId
				FROM ExpenseTransactions T
				INNER JOIN  Expenses E ON E.Id=T.ExpenseId
				INNER JOIN HeadTransactions h ON  h.Id = T.TransactionHeadId
				INNER JOIN dbo.HeadSubs s ON s.Id = h.SubHeadsId

				WHERE E.CompanyId = @CompanyId AND s.HeadId IN (5000) AND T.InvoiceAmount!=0 AND ((@IsRetain='1' OR E.ExpenseDate>=@StartDate1) AND E.ExpenseDate<=@EndDate1)



-- ================== Period 2 (optional comparison period) ==================
-- EF equivalent: handler invokes BuildPeriodAsync(...) a second time with isRetain=false.
-- The 6 buckets repeat with the same shape; only difference vs Period 1 is the date window
-- and that IsRetain is forced to false (Period 2 always enforces the start-date lower bound).
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