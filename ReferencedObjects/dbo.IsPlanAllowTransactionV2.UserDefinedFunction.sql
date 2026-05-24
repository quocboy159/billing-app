CREATE OR ALTER FUNCTION [dbo].[IsPlanAllowTransactionV2](@CompanyId INT,@FeatureId INT,@ModuleId INT,@UserId VARCHAR(128))  
RETURNS VARCHAR(300)  
AS   
BEGIN  
	DECLARE @AlertMessage VARCHAR(300)=null
	SET @AlertMessage = [dbo].[IsPlanAllowBulkTransactionsV2](@CompanyId,@FeatureId,@ModuleId,@UserId,1) 
	RETURN @AlertMessage;  
END;