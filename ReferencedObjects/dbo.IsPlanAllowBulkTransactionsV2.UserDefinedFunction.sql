SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER   FUNCTION [dbo].[IsPlanAllowBulkTransactionsV2](@CompanyId INT,@FeatureId INT,@ModuleId INT,@UserId VARCHAR(128), @BulkItemsCount INT)  
RETURNS VARCHAR(300)  
AS   
BEGIN  
DECLARE @IsAllow BIT='1';
DECLARE @MaxLimits INT=-1
DECLARE @Unlimited INT=10000
DECLARE @LimitCheckingDate datetime=cast('17530101' as datetime) -- MIN DATE
DECLARE @AlertMessage VARCHAR(300)=null
 
IF EXISTS	(SELECT 1 FROM PlanFeatureCompanies F 
				INNER JOIN PlanSubscriptions S ON S.Id=F.SubscriptionId 
				INNER JOIN PlanFeatureAlerts A ON A.FeatureId=F.FeatureId 
				WHERE S.IsActive = 1 and S.ToDate >= GETUTCDATE() and S.CompanyId=@CompanyId AND F.FeatureId=@FeatureId
			)
	BEGIN
		SELECT  
			@MaxLimits=CASE WHEN mf.IsRestricted = 1 then F.[Count] else (CASE WHEN (F.[Count]>0) THEN F.[Count] ELSE @Unlimited END) END,
			@LimitCheckingDate=DATEADD(day, -s.DurationDays, s.ToDate), 
			@AlertMessage=A.Alert 
			FROM PlanFeatureCompanies F 
			INNER JOIN PlanSubscriptions S 
			ON S.Id=F.SubscriptionId 
			INNER JOIN PlanFeatureAlerts A 
			ON A.FeatureId=F.FeatureId
			INNER JOIN PlanFeatureMasters mf
			ON mf.Id = f.FeatureId
		WHERE S.IsActive = 1 and S.ToDate >= GETUTCDATE() and S.CompanyId=@CompanyId AND F.FeatureId=@FeatureId
	END
ELSE
	BEGIN
	IF EXISTS(Select 1 FROM PlanFeatures F INNER JOIN Plans P ON P.Id=F.PlanId where P.IsFree=1 AND F.FeatureId=@FeatureId AND P.IsActive='1' AND F.IsEnable = 1 AND F.IsActive = 1)
	 	Select 
			@MaxLimits=CASE WHEN mf.IsRestricted = 1 then ISNULL(fl.Limit, 0) else (CASE WHEN (fl.Limit>0) THEN fl.Limit ELSE @Unlimited END) END,
			@AlertMessage=A.Alert   
			FROM PlanFeatures F 
			INNER JOIN PlanFeatureAlerts A 
			ON A.FeatureId=F.FeatureId  
			INNER JOIN Plans P 
			ON P.Id=F.PlanId
			INNER JOIN PlanFeatureMasters mf
			ON mf.Id = f.FeatureId
			LEFT JOIN PlanFeatureLimits fl on fl.PlanId = P.Id and fl.FeatureId = F.FeatureId and fl.DurationType is NULL
			where IsFree=1 AND F.FeatureId=@FeatureId AND IsEnable='1' and  P.IsFree=1 AND P.IsActive='1' AND F.IsActive = 1
	ELSE
	Select @IsAllow=CAST('0' as bit), @AlertMessage=(case when (IsActive = 1) then NAAlert else InactiveAlert end),@MaxLimits=-1 
	from PlanFeatureAlerts a inner join PlanFeatureMasters f on a.FeatureId = f.Id where FeatureId=@FeatureId
	END
	
IF @MaxLimits!=@Unlimited AND @MaxLimits!=0 AND @MaxLimits!=-1
	BEGIN
		IF @FeatureId=1 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM INVOICES WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=2 
			BEGIN
				IF @ModuleId=1
					SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM EstimateInvoices WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
				ELSE IF @ModuleId=2
					SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM RecurringInvoices WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=3 
			BEGIN
				IF @ModuleId=1
				BEGIN
					SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM ProductServices P
					INNER JOIN HeadTransactions H ON P.HeadTransactionId=H.Id
					INNER JOIN HeadSubs S ON H.SubHeadsId=S.Id
					WHERE P.CompanyId=@CompanyId AND S.HeadId=4000 AND P.Created>=@LimitCheckingDate
				END
				ELSE 
				BEGIN
					SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM ProductServices P
					INNER JOIN HeadTransactions H ON P.HeadTransactionId=H.Id
					INNER JOIN HeadSubs S ON H.SubHeadsId=S.Id
					WHERE P.CompanyId=@CompanyId AND S.HeadId=5000 AND P.Created>=@LimitCheckingDate
				END
			END
		ELSE IF @FeatureId=4 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM Bills WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=5 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM EstimateBills WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=6 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM Vendors WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=7 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM BankAccounts WHERE CompanyId=@CompanyId and IsPlaidAccount = 1 
			END
		ELSE IF @FeatureId=8 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM BankReconciles WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=9 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM HeadTransactions WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=10 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM INVOICES WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=11 
			BEGIN
				SET  @IsAllow='1'
			END
		ELSE IF @FeatureId=12 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM HeadTransactions WHERE CompanyId=@CompanyId AND  SubHeadsId=2004 and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=13 
			BEGIN
				SET  @IsAllow='1'
			END
		ELSE IF @FeatureId=14 
			BEGIN
				SET  @IsAllow='1'
			END
		ELSE IF @FeatureId=15 
			BEGIN
				SET  @IsAllow='1'
			END
		ELSE IF @FeatureId=16 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM CreditNotes WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=17 
			BEGIN
				SET  @IsAllow='1'
			END
		ELSE IF @FeatureId=18 
			BEGIN
				IF EXISTS(SELECT 1 FROM AspNetUsers U INNER JOIN AspNetUserRoles R ON U.id=R.UserId INNER JOIN Companies C  ON c.id=R.CompanyId  WHERE U.Id=@UserId AND  R.CompanyId=@CompanyId  AND R.IsOwner='1')
					BEGIN
						SELECT  @IsAllow=CASE WHEN (@MaxLimits+1)>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM AspnetUserRoles WHERE UserId=@UserId AND IsOwner='1'
					END
				ELSE
					BEGIN
						SET  @IsAllow='0'
					END
			END
		ELSE IF @FeatureId=19 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM Customers WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=20 
			BEGIN
				SELECT  @IsAllow=CASE WHEN (@MaxLimits+1)>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END FROM (select distinct UserId from AspnetUserRoles WHERE CompanyId=@CompanyId) usrs
			END
		ELSE IF @FeatureId=21 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM Expenses WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=22 
			BEGIN
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM RecurringBills WHERE CompanyId=@CompanyId and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=23 
			BEGIN
				DECLARE @BusinessCurrency VARCHAR(4)
				SELECT  @BusinessCurrency=BusinessCurrency FROM Companies WHERE Id=@CompanyId
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM INVOICES WHERE CompanyId=@CompanyId AND CustCurrency!=@BusinessCurrency and Created>=@LimitCheckingDate
			END
		ELSE IF @FeatureId=24 
			BEGIN
				DECLARE @BillBusinessCurrency VARCHAR(4)
				SELECT  @BillBusinessCurrency=BusinessCurrency FROM Companies WHERE Id=@CompanyId
				SELECT  @IsAllow=CASE WHEN @MaxLimits>=(COUNT(*)+@BulkItemsCount) THEN '1' ELSE '0' END  FROM Bills WHERE CompanyId=@CompanyId AND Currency!=@BillBusinessCurrency and Created>=@LimitCheckingDate
			END
		
	END	
ELSE IF (@MaxLimits=@Unlimited OR @MaxLimits=0) 
	SET @IsAllow='1';
ELSE
	SET @IsAllow='0'; 


IF @IsAllow='1'
	SET @AlertMessage=null;

   RETURN @AlertMessage;  
END;