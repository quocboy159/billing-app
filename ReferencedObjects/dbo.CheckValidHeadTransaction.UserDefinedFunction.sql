/****** Object:  UserDefinedFunction [dbo].[CheckValidHeadTransaction]    Script Date: 10/18/2023 8:17:41 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE  FUNCTION [dbo].[CheckValidHeadTransaction](@CompanyId INT,@HeadTransactionId INT)  
RETURNS BIT   
AS   
BEGIN  
	 DECLARE @Response BIT=0

	 IF EXISTS(SELECT 1 FROM HeadTransactions WHERE Id=@HeadTransactionId AND CompanyId=@CompanyId)
		BEGIN
			SET @Response=1
		END
		   
	RETURN @Response;  
END

GO
