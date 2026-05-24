
GO
/****** Object:  StoredProcedure [dbo].[BillsGetForExport]    Script Date: 10/18/2023 8:17:41 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[BillsGetForExport] --[dbo].[BillsGetForExport] 9
(
	@CompanyId int,
	@VendorId int=null,
	@Status int=null,
	@From datetime=null,
	@To datetime=null,
	@InvoiceNo varchar(15)=null,
	@FilterKeyword varchar(15)=null,
	@PageNumber INT = 1,
	@RecordsPerPage INT = 20,
	@PageRequestType INT = 1,
	@DeletedInvoiceDisplayFor INT = 0
)
AS
BEGIN

	SET NOCOUNT ON;
	    DECLARE @RequestDefault INT = 1, @RequestNavigation INT = 2;
		DECLARE @Draft INT=0, @Approved INT=100, @ParitalSettled INT=150, @Settled INT=200,@Deleted INT=500;;
		DECLARE @OverdueStatus INT=1001,@DeletedStatus INT=2000,@ArchivedStatus INT=3000;;
		
		DECLARE @tblTaxes TABLE([BillId] [int] NOT NULL,[Tax] [DECIMAL](18,4))
		DECLARE @tblDiscounts TABLE([BillId] [int] NOT NULL,[SubTotal] [DECIMAL](18,4))
		
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
		AND  ((@FilterKeyword IS NULL) OR ((InvoiceNo LIKE '%' + @FilterKeyword + '%') OR (V.VendorName LIKE '%' + @FilterKeyword + '%')))
		GROUP BY B.[Id],B.[Guid],B.[HeadTransactionVendorId],V.VendorName,B.[Status],B.[InvoiceNo],B.[BillAt],B.[DueAt],B.[Price],B.Currency,B.Notes,TT.Tax,D.SubTotal
		ORDER BY B.Id DESC
	
				
		
    SET NOCOUNT OFF;

END




GO
