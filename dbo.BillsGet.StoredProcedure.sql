
GO
/****** Object:  StoredProcedure [dbo].[BillsGet]    Script Date: 10/18/2023 8:17:41 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER PROCEDURE [dbo].[BillsGet]
    @Guid VARCHAR(70),
    @CompanyId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @BillId int;
    DECLARE @PaidAmount decimal(18,4)=0.0;

    SELECT @BillId=Id FROM Bills WHERE [Guid]=@Guid AND CompanyId=@CompanyId
    IF @BillId>0
        BEGIN
            IF EXISTS(SELECT 1 from BillPaymentDetails WHERE BillId= @BillId)
                SELECT @PaidAmount=SUM(ISNULL(Amount,0)) from BillPaymentDetails WHERE BillId= @BillId

            SELECT B.*,(B.Price-@PaidAmount) As DueAmount, 0 AS PaymentDueDays,(CASE WHEN BM.Id IS NULL THEN 0 ELSE 1 END)[IsAutoCreated]
            FROM Bills B LEFT JOIN RecurringBillMaps BM ON B.Id=BM.BillId
            WHERE B.Id=@BillId

            SELECT i.Id,i.ProductId,p.Name As ProductName,i.Description, sh.Id as SubHeadId,sh.Name as SubHeadName, i.ProductTransactionHeadId
                 ,th.Name as TransactionHeadName,i.Quantity,i.Price
            FROM BillTransactions i INNER JOIN ProductServices p
                                               ON i.ProductId=p.id INNER JOIN HeadTransactions th
                                                                              ON i.ProductTransactionHeadId=th.id INNER JOIN HeadSubs sh
                                                                                                                             ON th.SubHeadsId=sh.Id
            WHERE i.BillId=@BillId AND i.CompanyId=@CompanyId

            SELECT * FROM BillTransactionTaxes WHERE CompanyId=@CompanyId AND BillTransactionId in(SELECT Id FROM BillTransactions WHERE BillId=@BillId AND CompanyId=@CompanyId)

            SELECT c.Id,c.HeadTransactionId,c.CompanyId,c.FirstName,c.LastName,c.VendorName,c.AccountNumber,c.Email,c.Currency,c.BillCountry,c.BillState,c.BillCity,c.BillAddress1,c.BillAddress2,c.BillPostCode,c.Phone,c.Mobile,c.Fax,c.TollFree,c.Portal,c.CorporateIdNumber,c.VatIdNumber
            FROM Vendors c INNER JOIN Bills i
                                      ON c.HeadTransactionId=i.HeadTransactionVendorId
            WHERE i.Id=@BillId AND c.CompanyId=@CompanyId
        END
END


GO
