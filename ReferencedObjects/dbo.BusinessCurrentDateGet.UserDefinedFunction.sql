/****** Object:  UserDefinedFunction [dbo].[BusinessCurrentDateGet]    Script Date: 10/18/2023 8:17:41 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE  FUNCTION [dbo].[BusinessCurrentDateGet](@CompanyId INT)  
RETURNS Datetime   
AS   
BEGIN  
DECLARE @OffsetUTCAS INT=0;
DECLARE @CurrentDate AS Datetime
SELECT @OffsetUTCAS=ISNULL(OffsetUTC,0) FROM Companies WHERE id=@CompanyId
SET @CurrentDate=CAST(DATEADD(minute,@OffsetUTCAS,GETUTCDATE()) as date)
SET @CurrentDate=DATEADD(SECOND,86399,@CurrentDate)
RETURN @CurrentDate;  
END

GO
