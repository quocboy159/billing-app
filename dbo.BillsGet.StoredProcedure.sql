
GO
/****** Object:  StoredProcedure [dbo].[BillsGet]    Script Date: 10/18/2023 8:17:41 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* =============================================================================================
   EF Core port :  src/Application/Bills/GetByGuid/GetBillByGuidQueryHandler.cs
   Endpoint     :  GET /bills/{guid}?companyId=...   (src/Web.Api/Endpoints/Bills/GetByGuid.cs)
   Query DTO    :  GetBillByGuidQuery -> returns BillDetailResponse (header + lines + taxes + vendor)
   Unit tests   :  tests/UnitTests/Bills/GetByGuid/GetBillByGuidQueryHandlerTests.cs

   The SP returns 4 result sets to the caller; the EF handler folds them into a single
   BillDetailResponse so the client receives one JSON body.

   Step-by-step mapping (each step is also commented inline below):
     1. Resolve BillId from (Guid, CompanyId)  -> projection in the handler:
                                                    from b in context.Bills.AsNoTracking()
                                                    where b.Guid == query.Guid && b.CompanyId == query.CompanyId
                                                    select new {
                                                      Bill = b,
                                                      PaidAmount   = context.BillPaymentDetails
                                                                            .Where(p => p.BillId == b.Id)
                                                                            .Sum(p => (decimal?)p.Amount) ?? 0m,
                                                      IsAutoCreated = context.RecurringBillMaps.Any(...)
                                                    }
     2. Header result set                      -> mapped into BillDetailResponse.Header above
     3. Lines (Product + Head + SubHead joins) -> 2nd LINQ projection -> List<BillDetailResponse.BillLine>
     4. Taxes for those lines                  -> 3rd query, filtered by the line Ids we already have
     5. Vendor summary                         -> 4th query -> BillDetailResponse.VendorSummary
     6. NotFound case                          -> Result.Failure<>(BillErrors.NotFound(query.Guid))
   ============================================================================================= */
ALTER PROCEDURE [dbo].[BillsGet]
    @Guid VARCHAR(70),
    @CompanyId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @BillId int;
    DECLARE @PaidAmount decimal(18,4)=0.0;

    -- Step 1: resolve BillId by (Guid, CompanyId).
    -- EF equivalent: filter `b.Guid == query.Guid && b.CompanyId == query.CompanyId`
    -- in the header projection; SingleOrDefaultAsync == null -> Result.Failure(BillErrors.NotFound).
    SELECT @BillId=Id FROM Bills WHERE [Guid]=@Guid AND CompanyId=@CompanyId
    IF @BillId>0
        BEGIN
            -- Step 1b: aggregate paid amount.
            -- EF equivalent: context.BillPaymentDetails.Where(p => p.BillId == b.Id)
            --                                          .Sum(p => (decimal?)p.Amount) ?? 0m
            -- The nullable Sum + ?? replaces the explicit EXISTS + ISNULL pattern.
            IF EXISTS(SELECT 1 from BillPaymentDetails WHERE BillId= @BillId)
                SELECT @PaidAmount=SUM(ISNULL(Amount,0)) from BillPaymentDetails WHERE BillId= @BillId

            -- Step 2: result set #1 - bill header with computed DueAmount and IsAutoCreated.
            -- EF equivalent: BillDetailResponse.Header { ...,
            --                                            DueAmount = b.Price - PaidAmount,
            --                                            IsAutoCreated = context.RecurringBillMaps.Any(m => m.BillId == b.Id) }
            SELECT B.*,(B.Price-@PaidAmount) As DueAmount, 0 AS PaymentDueDays,(CASE WHEN BM.Id IS NULL THEN 0 ELSE 1 END)[IsAutoCreated]
            FROM Bills B LEFT JOIN RecurringBillMaps BM ON B.Id=BM.BillId
            WHERE B.Id=@BillId

            -- Step 3: result set #2 - line items joined with ProductServices / HeadTransactions / HeadSubs.
            -- EF equivalent: LINQ query in the handler that .Select-s into BillDetailResponse.BillLine
            -- using `join` clauses on ProductServices, HeadTransactions, HeadSubs.
            SELECT i.Id,i.ProductId,p.Name As ProductName,i.Description, sh.Id as SubHeadId,sh.Name as SubHeadName, i.ProductTransactionHeadId
                 ,th.Name as TransactionHeadName,i.Quantity,i.Price
            FROM BillTransactions i INNER JOIN ProductServices p
                                               ON i.ProductId=p.id INNER JOIN HeadTransactions th
                                                                              ON i.ProductTransactionHeadId=th.id INNER JOIN HeadSubs sh
                                                                                                                             ON th.SubHeadsId=sh.Id
            WHERE i.BillId=@BillId AND i.CompanyId=@CompanyId

            -- Step 4: result set #3 - taxes attached to those line items.
            -- EF equivalent: context.BillTransactionTaxes.Where(t => t.CompanyId == query.CompanyId
            --                                                     && lineIds.Contains(t.BillTransactionId))
            -- We pass the materialised line Id array instead of re-running the BillTransactions sub-select.
            SELECT * FROM BillTransactionTaxes WHERE CompanyId=@CompanyId AND BillTransactionId in(SELECT Id FROM BillTransactions WHERE BillId=@BillId AND CompanyId=@CompanyId)

            -- Step 5: result set #4 - vendor summary linked via Bills.HeadTransactionVendorId.
            -- EF equivalent: context.Vendors.Where(v => v.HeadTransactionId == header.Bill.HeadTransactionVendorId
            --                                       && v.CompanyId == query.CompanyId)
            --                              .Select(v => new BillDetailResponse.VendorSummary { ... })
            SELECT c.Id,c.HeadTransactionId,c.CompanyId,c.FirstName,c.LastName,c.VendorName,c.AccountNumber,c.Email,c.Currency,c.BillCountry,c.BillState,c.BillCity,c.BillAddress1,c.BillAddress2,c.BillPostCode,c.Phone,c.Mobile,c.Fax,c.TollFree,c.Portal,c.CorporateIdNumber,c.VatIdNumber
            FROM Vendors c INNER JOIN Bills i
                                      ON c.HeadTransactionId=i.HeadTransactionVendorId
            WHERE i.Id=@BillId AND c.CompanyId=@CompanyId
        END
END


GO
