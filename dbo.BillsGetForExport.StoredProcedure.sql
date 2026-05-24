
GO
/****** Object:  StoredProcedure [dbo].[BillsGetForExport]    Script Date: 10/18/2023 8:17:41 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* =============================================================================================
   EF Core port :  src/Application/Bills/GetForExport/GetBillsForExportQueryHandler.cs
   Endpoint     :  POST /bills/export        (src/Web.Api/Endpoints/Bills/GetForExport.cs)
   Query DTO    :  GetBillsForExportQuery -> returns List<BillExportRowResponse>
   Unit tests   :  tests/UnitTests/Bills/GetForExport/GetBillsForExportQueryHandlerTests.cs

   Step-by-step mapping (each step is also commented inline below):
     1. Status / IsArchive / Deleted-window filter
                                              -> BuildStatusPredicate(...) returns one
                                                 Expression<Func<Bill,bool>>; the SP repeats the
                                                 5-branch OR three times (taxes, discounts, main)
                                                 -- in EF we evaluate it once and reuse.
     2. @tblTaxes (per-bill SUM(Qty*Price*Rate/100))
                                              -> inlined as a correlated SUM in the main projection:
                                                 TotalTax = context.BillTransactions
                                                                   .Where(t => t.BillId == b.Id)
                                                                   .SelectMany(t => context.BillTransactionTaxes
                                                                                            .Where(tt => tt.BillTransactionId == t.Id)
                                                                                            .Select(tt => (decimal?)(t.Quantity * t.Price * tt.Rate / 100m)))
                                                                   .Sum() ?? 0m
     3. @tblDiscounts (per-bill SUM(Qty*Price))
                                              -> inlined as another correlated SUM:
                                                 SubTotal = context.BillTransactions
                                                                   .Where(t => t.BillId == b.Id)
                                                                   .Sum(t => (decimal?)(t.Quantity * t.Price)) ?? 0m
     4. Optional filters (VendorId, From, To, InvoiceNo)
                                              -> conditional `.Where(...)` chained onto the queryable
     5. FilterKeyword (LIKE InvoiceNo OR LIKE VendorName)
                                              -> EF.Functions.Like(b.InvoiceNo, "%kw%") OR Vendor.VendorName Like
     6. Vendor join + ORDER BY Id DESC + projection
                                              -> LEFT JOIN with DefaultIfEmpty(), then Select to
                                                 BillExportRowResponse, OrderByDescending(b.Id)
     7. Pagination (@PageNumber, @RecordsPerPage)
                                              -> .Skip((page-1)*size).Take(size)
                                                 NB: the SP declares the params but never uses them;
                                                 the EF handler implements proper paging.
     8. DueAmount = Price - SUM(payments)
                                              -> correlated SUM of BillPaymentDetails inside the
                                                 same Select projection.

   Refactor wins vs the SP:
     - the 5-branch status OR is written once (BuildStatusPredicate), not three times
     - @tblTaxes / @tblDiscounts table variables disappear -- one SELECT pass instead of three
     - DATEDIFF(day, Deleted, GETUTCDATE()) <= @DeletedInvoiceDisplayFor becomes a pre-computed
       `deletedCutoff = DateTime.UtcNow.AddDays(-DeletedInvoiceDisplayFor)` and `Deleted >= cutoff`,
       so the predicate is provider-neutral and SARGable on (Deleted).
   ============================================================================================= */
CREATE PROCEDURE [dbo].[BillsGetForExport] --[dbo].[BillsGetForExport] 9
(
	@CompanyId int,
	@VendorId int=null,
	@Status int=null,
	@From datetime=null,
	@To datetime=null,
	@InvoiceNo varchar(15)=null,
	@FilterKeyword varchar(15)=null,
	@PageNumber INT = 1,           -- unused in SP; honoured in the EF handler via Skip/Take
	@RecordsPerPage INT = 20,      -- unused in SP; honoured in the EF handler via Skip/Take
	@PageRequestType INT = 1,      -- unused in SP; not modelled in the EF query
	@DeletedInvoiceDisplayFor INT = 0
)
AS
BEGIN

	SET NOCOUNT ON;
	    -- Status sentinels: the BillStatus enum (Draft/Approved/PartiallySettled/Settled/Deleted)
	    -- and BillFilterStatus enum (Overdue/Deleted/Archived) live in Domain/Bills.
	    DECLARE @RequestDefault INT = 1, @RequestNavigation INT = 2;
		DECLARE @Draft INT=0, @Approved INT=100, @ParitalSettled INT=150, @Settled INT=200,@Deleted INT=500;;
		DECLARE @OverdueStatus INT=1001,@DeletedStatus INT=2000,@ArchivedStatus INT=3000;;

		-- Steps 2 & 3: table variables that pre-aggregate taxes and subtotals per bill.
		-- EF eliminates these temp tables -- both sums are inlined as correlated sub-selects
		-- in the single main projection (see Step 6 below).
		DECLARE @tblTaxes TABLE([BillId] [int] NOT NULL,[Tax] [DECIMAL](18,4))
		DECLARE @tblDiscounts TABLE([BillId] [int] NOT NULL,[SubTotal] [DECIMAL](18,4))

		-- Step 2 (SP): per-bill tax aggregation. EF inlines this as a correlated SUM (see Step 6).
		INSERT INTO @tblTaxes([BillId],[Tax])
		SELECT T.BillId,Sum((T.Quantity*T.Price) * ISNULL(TT.Rate,0)/100) as Tax FROM Bills B 
		LEFT JOIN BillTransactions T ON B.Id=T.BillId
		LEFT JOIN BillTransactionTaxes TT ON T.Id=TT.BillTransactionId
		WHERE B.CompanyId=@CompanyId
		AND  ((@VendorId IS NULL) OR (HeadTransactionVendorId=@VendorId))
		AND  ((@Status IS NULL AND [Status]!=@Deleted AND [IsArchive]=0) OR (@Status=@OverdueStatus AND B.Status IN(@Approved,@ParitalSettled) AND B.DueAt<=[dbo].[BusinessCurrentDateGet](@CompanyId) AND [Status]!=@Deleted AND [IsArchive]=0) OR (@Status=@DeletedStatus AND [Status]=@Deleted AND ISNULL(DATEDIFF(day,[Deleted], GETUTCDATE()),0)<=@DeletedInvoiceDisplayFor) OR(@Status=@ArchivedStatus AND [IsArchive]=1) OR ([Status]=@Status AND [Status]!=@Deleted AND [IsArchive]=0))
		AND  ((@From IS NULL) OR (B.Created>=@From))
		AND  ((@To IS NULL) OR (B.Created<=@To))
		AND  ((@InvoiceNo IS NULL) OR (InvoiceNo=@InvoiceNo))		
		Group By T.BillId
		
		-- Step 3 (SP): per-bill subtotal aggregation. EF inlines this too (see Step 6).
		INSERT INTO @tblDiscounts([BillId],[SubTotal])
		SELECT T.BillId,Sum(T.Quantity*T.Price)[SubTotal] FROM Bills B
		LEFT JOIN BillTransactions T ON B.Id=T.BillId
		WHERE B.CompanyId=@CompanyId
		AND  ((@VendorId IS NULL) OR (HeadTransactionVendorId=@VendorId))
		AND  ((@Status IS NULL AND [Status]!=@Deleted AND [IsArchive]=0) OR (@Status=@OverdueStatus AND B.Status IN(@Approved,@ParitalSettled) AND B.DueAt<=[dbo].[BusinessCurrentDateGet](@CompanyId) AND [Status]!=@Deleted AND [IsArchive]=0) OR (@Status=@DeletedStatus AND [Status]=@Deleted AND ISNULL(DATEDIFF(day,[Deleted], GETUTCDATE()),0)<=@DeletedInvoiceDisplayFor) OR(@Status=@ArchivedStatus AND [IsArchive]=1) OR ([Status]=@Status AND [Status]!=@Deleted AND [IsArchive]=0))
		AND  ((@From IS NULL) OR (B.Created>=@From))
		AND  ((@To IS NULL) OR (B.Created<=@To))
		AND  ((@InvoiceNo IS NULL) OR (InvoiceNo=@InvoiceNo))		
		Group By T.BillId		

		-- Step 6: the main projection.
		-- EF equivalent (handler):
		--     var projected =
		--         from b in bills                                         -- with all .Where(...) filters already applied
		--         join v in context.Vendors on b.HeadTransactionVendorId equals v.HeadTransactionId
		--             into vendorJoin
		--         from v in vendorJoin.DefaultIfEmpty()
		--         orderby b.Id descending
		--         select new BillExportRowResponse {
		--             Id = b.Id, Guid = b.Guid, ...
		--             DueAmount = b.Price - context.BillPaymentDetails
		--                                         .Where(p => p.BillId == b.Id)
		--                                         .Sum(p => (decimal?)p.Amount) ?? 0m,
		--             SubTotal = ... (correlated SUM, replacing @tblDiscounts),
		--             TotalTax = ... (correlated SUM with SelectMany, replacing @tblTaxes)
		--         };
		SELECT  B.[Id],B.[Guid],B.[HeadTransactionVendorId],V.VendorName,B.[Status],B.[InvoiceNo],B.[BillAt],B.[DueAt],B.[Price],DueAmount=(B.[Price] - ISNULL(SUM(P.Amount),0)),B.Currency,B.Notes,ISNULL(TT.Tax,0)[TotalTax],ISNULL(D.SubTotal,0)[SubTotal]
		FROM Bills B
		LEFT JOIN BillPaymentDetails P ON B.Id=P.BillId
		LEFT JOIN Vendors V ON B.HeadTransactionVendorId=V.HeadTransactionId
		LEFT JOIN @tblTaxes TT ON B.Id=TT.BillId
		LEFT JOIN @tblDiscounts D ON B.Id=D.BillId
		WHERE B.CompanyId=@CompanyId
		AND  ((@VendorId IS NULL) OR (HeadTransactionVendorId=@VendorId))
		AND  ((@Status IS NULL AND [Status]!=@Deleted AND [IsArchive]=0) OR (@Status=@OverdueStatus AND B.Status IN(@Approved,@ParitalSettled) AND B.DueAt<=[dbo].[BusinessCurrentDateGet](@CompanyId) AND [Status]!=@Deleted AND [IsArchive]=0) OR (@Status=@DeletedStatus AND [Status]=@Deleted AND ISNULL(DATEDIFF(day,[Deleted], GETUTCDATE()),0)<=@DeletedInvoiceDisplayFor) OR(@Status=@ArchivedStatus AND [IsArchive]=1) OR ([Status]=@Status AND [Status]!=@Deleted AND [IsArchive]=0))
		AND  ((@From IS NULL) OR (B.Created>=@From))
		AND  ((@To IS NULL) OR (B.Created<=@To))
		AND  ((@InvoiceNo IS NULL) OR (InvoiceNo=@InvoiceNo))
		-- Step 5: keyword filter on InvoiceNo OR VendorName.
		-- EF equivalent: EF.Functions.Like(b.InvoiceNo, "%kw%") OR EF.Functions.Like(v.VendorName, "%kw%")
		AND  ((@FilterKeyword IS NULL) OR ((InvoiceNo LIKE '%' + @FilterKeyword + '%') OR (V.VendorName LIKE '%' + @FilterKeyword + '%')))
		-- GROUP BY is needed here because BillPaymentDetails is joined directly (no pre-aggregate).
		-- EF replaces this with a correlated SUM in the projection, so no GROUP BY is needed.
		GROUP BY B.[Id],B.[Guid],B.[HeadTransactionVendorId],V.VendorName,B.[Status],B.[InvoiceNo],B.[BillAt],B.[DueAt],B.[Price],B.Currency,B.Notes,TT.Tax,D.SubTotal
		-- Step 7 (EF only): .Skip((page-1)*size).Take(size) for pagination.
		ORDER BY B.Id DESC
	
				
		
    SET NOCOUNT OFF;

END




GO
