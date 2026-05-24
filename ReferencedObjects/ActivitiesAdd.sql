SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER   PROCEDURE [dbo].[ActivitiesAdd]
 @CompanyId AS INT
,@TableTransactionId AS INT
,@TableTransactionValue AS VARCHAR(100)=NULL
,@TableEnumType AS INT
,@ActionType AS INT
,@TransactionHeadId AS INT
,@Remarks AS VARCHAR(500)
,@UserName AS varchar(256)=NULL
AS
BEGIN

	SET NOCOUNT ON;
	BEGIN TRY
             BEGIN TRANSACTION
			 SAVE TRANSACTION SavePoint;
				IF @CompanyId>0
					BEGIN
						DECLARE @TableTypeInvoice INT=100,@TableTypeEstimate INT=200,@TableTypeRecurring INT=300, @TableTypeBill INT=400,@TableTypeRecBill INT=500,@TableTypePurchaseOrder INT=600,@TableTypeBanks INT=800,@TableTypeReconciliation INT=900,@TableTypeCreditNote INT=1800
						DECLARE @AcnTypeInvCrted INT=101, @AcnTypeInvUpdtd INT=102, @AcnTypeInvSent INT=103, @AcnTypeInvReSent INT=104, @AcnTypeInvApproved INT=105,@AcnTypeInvDeleteRollback INT=111,@AcnTypeInvArchiveRollback INT=113
						DECLARE @AcnTypeCreditNoteCreate INT=125, @AcnTypeCreditNoteUpdate INT=126, @AcnTypeCreditNoteInvoiced INT=127, @AcnTypeCreditNoteRollback INT=128, @AcnTypeCreditNoteDelete INT=129, @AcnTypeCreditNoteDeleteRollback INT=130, @AcnTypeCreditNotePermanentDelete INT=131
						DECLARE @AcnTypeEstCrted INT=201, @AcnTypeEstUpdtd INT=202, @AcnTypeEstSent INT=203, @AcnTypeEstReSent INT=204, @AcnTypeEstApproved INT=205, @AcnTypeEstConverted INT=206, @AcnTypeEstDelete INT=207, @AcnTypeEstRollback INT=208
						DECLARE @AcnTypeRecCrted INT=301, @AcnTypeRecUpdtd INT=302, @AcnTypeRecSent INT=303, @AcnTypeAutoInvCretd INT=304, @AcnTypeRecApproved INT=305, @AcnTypeRecDelete INT=306, @AcnTypeRecRollback INT=307, @AcnTypeRecPaused INT=308, @AcnTypeRecResumed INT=309							
						DECLARE @AcnTypeBillCrted INT=401, @AcnTypeBillUpdtd INT=402, @AcnTypeBillApproved INT=403, @AcnTypeBillArchiveRollback INT=407, @AcnTypeBillDeleteRollback INT=405
						DECLARE @AcnTypeRecBillCrted INT=501, @AcnTypeRecBillUpdtd INT=502, @AcnTypeRecBillSent INT=503, @AcnTypeAutoBillCretd INT=504, @AcnTypeRecBillApproved INT=505, @AcnTypeRecBillDelete INT=506
						DECLARE @AcnTypePurchaseOrderCrted INT=601, @AcnTypePurchaseOrderUpdtd INT=602, @AcnTypePurchaseOrderApproved INT=603, @AcnTypePurchaseOrderConverted INT=604, @AcnTypePurchaseOrderDelete INT=605, @AcnTypePurchaseOrderRollback INT=606, @AcnTypePurchaseOrderRejected INT=607
						DECLARE @AcnTypeBankTransactionAdd INT=804,@AcnTypeBankTransactionEdit INT=805,@AcnTypeBankDelete INT=807
						DECLARE @AcnTypeReconciliationAdd INT=901,@AcnTypeReconciliationTransactionAdd INT=902,@AcnTypeReconciliationTransactionDelete INT=903
						DECLARE @AcnBankDelete INT=1705
						DECLARE @AcnBankDeactivate INT=1706
						DECLARE @AcnBankActivate INT=1707

						DECLARE @PrepareRemarks AS VARCHAR(500)
						DECLARE @TransactionHeadName AS VARCHAR(250)=NULL
						IF @TransactionHeadId>0
							BEGIN
								SELECT @TransactionHeadName=Name FROM HeadTransactions Where Id=@TransactionHeadId AND CompanyId=@CompanyId
							END

						SET @PrepareRemarks=(CASE WHEN @TableEnumType = @TableTypeInvoice THEN 'INV' ELSE
									 CASE WHEN @TableEnumType = @TableTypeEstimate THEN 'EST' ELSE
									 CASE WHEN @TableEnumType = @TableTypeRecurring THEN 'REC' ELSE
									 CASE WHEN @TableEnumType = @TableTypeBill THEN 'BILL' ELSE 
									 CASE WHEN @TableEnumType = @TableTypePurchaseOrder THEN 'PO' ELSE
									 CASE WHEN @TableEnumType = @TableTypeBanks THEN 'BANKS' ELSE
									 CASE WHEN @TableEnumType = @TableTypeReconciliation THEN 'RECONCILIATION' ELSE
									 CASE WHEN @TableEnumType = @TableTypeCreditNote THEN 'CREDITNOTE' ELSE
									 CASE WHEN @TableEnumType = @TableTypeRecBill THEN 'REC BILL' ELSE '' END END END END END END END END END									
									) + CASE WHEN @TableTransactionValue IS NOT NULL THEN '/' + @TableTransactionValue ELSE '' END +
									
									(CASE WHEN (@ActionType = @AcnTypeInvCrted OR @ActionType = @AcnTypeBillCrted OR @ActionType = @AcnTypeEstCrted OR @ActionType = @AcnTypeRecCrted OR @ActionType = @AcnTypeRecBillCrted OR @ActionType = @AcnTypePurchaseOrderCrted OR @ActionType = @AcnTypeCreditNoteCreate) THEN '/CREATED' ELSE
									 CASE WHEN (@ActionType = @AcnTypeInvUpdtd OR @ActionType = @AcnTypeBillUpdtd OR @ActionType = @AcnTypeEstUpdtd OR @ActionType = @AcnTypeRecUpdtd OR @ActionType = @AcnTypeRecBillUpdtd OR @ActionType = @AcnTypePurchaseOrderUpdtd OR @ActionType = @AcnTypeCreditNoteUpdate) THEN '/UPDATED' ELSE
									 CASE WHEN (@ActionType = @AcnTypeInvApproved OR @ActionType = @AcnTypeEstApproved OR @ActionType = @AcnTypeRecApproved OR @ActionType = @AcnTypeBillApproved OR @ActionType = @AcnTypeRecBillApproved OR @ActionType = @AcnTypePurchaseOrderApproved) THEN '/APPROVED' ELSE
									 CASE WHEN (@ActionType = @AcnTypeInvSent OR @ActionType = @AcnTypeEstSent ) THEN '/MAIL SENT' ELSE
									 CASE WHEN (@ActionType = @AcnTypeInvReSent OR @ActionType = @AcnTypeEstReSent) THEN '/MAIL RESENT' ELSE									 
									 CASE WHEN (@ActionType = @AcnTypeEstConverted) THEN '/EST CONVERTED' ELSE
									 CASE WHEN (@ActionType = @AcnTypePurchaseOrderConverted) THEN '/PO CONVERTED' ELSE
									 CASE WHEN (@ActionType = @AcnTypeAutoInvCretd) THEN '/SCD. INV CREATED' ELSE
									 CASE WHEN (@ActionType = @AcnTypeAutoBillCretd) THEN '/SCD. BILL CREATED' ELSE
									 
									 CASE WHEN (@ActionType = @AcnTypeRecSent) THEN '/AUTO MAIL SENT' 
									 ELSE '' END END END END END END END END END END 
									) +
									(CASE WHEN (@ActionType = @AcnTypeRecPaused) THEN '/PAUSED' ELSE
									 CASE WHEN (@ActionType = @AcnTypeRecResumed) THEN '/RESUMED' 
									 ELSE '' END END)
									+
									(CASE WHEN (@ActionType = @AcnTypeInvDeleteRollback OR @ActionType = @AcnTypeBillDeleteRollback OR @ActionType = @AcnTypeCreditNoteDeleteRollback) THEN '/DELETE ROLLBACK' ELSE
									 CASE WHEN (@ActionType = @AcnTypeRecDelete OR @ActionType = @AcnTypeEstDelete OR @ActionType = @AcnTypePurchaseOrderDelete OR @ActionType = @AcnTypeRecBillDelete OR @ActionType = @AcnTypeCreditNoteDelete) THEN '/DELETE' ELSE
									 CASE WHEN (@ActionType = @AcnTypeReconciliationAdd) THEN '/RECONC-INITIATED' ELSE
									 CASE WHEN (@ActionType = @AcnTypeRecRollback OR @ActionType = @AcnTypeEstRollback OR @ActionType = @AcnTypeCreditNoteRollback) THEN '/ROLLBACK' ELSE
									 CASE WHEN (@ActionType = @AcnTypeBankTransactionAdd OR @ActionType = @AcnTypeReconciliationTransactionAdd) THEN '/TRANSACTION ADD' ELSE
									 CASE WHEN (@ActionType = @AcnTypeBankTransactionEdit) THEN '/TRANSACTION UPDATE' ELSE									 
									 CASE WHEN (@ActionType = @AcnTypeBankDelete OR @ActionType = @AcnTypeReconciliationTransactionDelete) THEN '/TRANSACTION DELETE' ELSE
									 CASE WHEN (@ActionType = @AcnTypeInvArchiveRollback OR @ActionType = @AcnTypeBillArchiveRollback) THEN '/ARCHIVE ROLLBACK' ELSE
									 CASE WHEN (@ActionType = @AcnTypeCreditNoteInvoiced) THEN '/CREDITNOTE INVOICED' ELSE
									 CASE WHEN (@ActionType = @AcnBankDelete) THEN '/BANK ACCOUNT DELETE'
									 ELSE ''  END END END END END END END END END END
									)
									+
									(CASE WHEN (@ActionType = @AcnBankDeactivate) THEN '/BANK ACCOUNT DEACTIVATE' ELSE
									 CASE WHEN (@ActionType = @AcnBankDeactivate) THEN '/BANK ACCOUNT ACTIVATE' ELSE
									 CASE WHEN (@ActionType = @AcnTypePurchaseOrderRollback) THEN '/ROLLBACK' ELSE
									 CASE WHEN (@ActionType = @AcnTypePurchaseOrderRejected) THEN '/REJECT'  ELSE
									 CASE WHEN (@ActionType = @AcnTypeCreditNotePermanentDelete) THEN '/PERMANENT DELETE' 
									 ELSE '' END END  END END END)
									
									+ CASE WHEN @TransactionHeadName IS NOT NULL THEN '/' + UPPER(@TransactionHeadName) ELSE '' END 
									+ CASE WHEN @Remarks IS NOT NULL THEN '/' + @Remarks ELSE '' END 
									 
						
						INSERT INTO [dbo].[Activities] ([CompanyId],[TableTransactionId],[TableTransactionValue],[TableEnumType],[ActionType],[TransactionHeadName],[Remarks],[UserName],[Created])
						VALUES (@CompanyId,@TableTransactionId,@TableTransactionValue,@TableEnumType,@ActionType,@TransactionHeadName,SUBSTRING(@PrepareRemarks,1,500),@UserName,GETUTCDATE())

					END	
			COMMIT TRANSACTION
	END TRY    
	BEGIN CATCH  
	IF @@TRANCOUNT > 0
        BEGIN
		
		ROLLBACK TRANSACTION SavePoint;
		END
	END CATCH
END;