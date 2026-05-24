ALTER PROCEDURE [dbo].[BillsAdd]
    @tblBills tblBills READONLY,
    @tblBillTransactions tblInvoiceTransactions READONLY,
    @tblBillTransactionTaxes tblInvoicesTransactionTaxes READONLY,
    @UserName AS VARCHAR(256) = NULL,
    @tblVendor tblVendor READONLY
AS
BEGIN
    DECLARE 
        @CompanyId INT,
        @BillId INT = 0,
        @InvoiceNo VARCHAR(50),
        @HeadTransactionVendorId INT,
        @IsCustomerExist BIT = 0,
        @VendorCurrency VARCHAR(3),
        @CompanyCurrency VARCHAR(3),
        @AutoNumber INT,
        @Prefix VARCHAR(10) = NULL,
        @Sufffix VARCHAR(10) = NULL,
        @AnyAlterMsg VARCHAR(300);

    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    BEGIN TRY
        SELECT 
            @CompanyId = CompanyId,
            @HeadTransactionVendorId = HeadTransactionVendorId,
            @VendorCurrency = Currency 
        FROM @tblBills;

        SET @AnyAlterMsg = [dbo].[IsPlanAllowTransactionV2](@CompanyId, 4, 0, '');

        IF @AnyAlterMsg IS NULL
        BEGIN
            SELECT @CompanyCurrency = BusinessCurrency 
            FROM Companies 
            WHERE Id = @CompanyId;

            SET @AnyAlterMsg = [dbo].[IsPlanAllowTransactionV2](@CompanyId, 24, 0, '');

            IF @VendorCurrency IS NULL 
                OR @CompanyCurrency = @VendorCurrency 
                OR (@CompanyCurrency != @VendorCurrency AND @AnyAlterMsg IS NULL)
            BEGIN
                IF @HeadTransactionVendorId = 0 AND EXISTS(SELECT 1 FROM @tblVendor)
                BEGIN
                    DECLARE 
                        @VendorName VARCHAR(100) = NULL,
                        @AccountNumber VARCHAR(60) = NULL,
                        @Currency CHAR(3) = NULL;

                    SELECT 
                        @VendorName = VendorName,
                        @AccountNumber = AccountNumber,
                        @Currency = Currency 
                    FROM @tblVendor;

                    IF NOT EXISTS(SELECT 1 FROM Vendors WHERE CompanyId = @CompanyId AND VendorName = @VendorName)
                    BEGIN                        
                        INSERT INTO HeadTransactions 
                            ([CompanyId], [SubHeadsId], [AccountTaxId], [Name], [Currency])
                        VALUES
                            (@CompanyId, 2003, @AccountNumber, @VendorName, @Currency);

                        SET @HeadTransactionVendorId = SCOPE_IDENTITY();

                        INSERT INTO [dbo].[Vendors]
                            ([HeadTransactionId], [CompanyId], [FirstName], [LastName], [VendorName], [AccountNumber], [Email], [Currency], [BillCountry], [BillState], [BillCity], [BillAddress1], [BillAddress2], [BillPostCode], [Phone], [Mobile], [Fax], [TollFree], [Portal], [IsActive], [Created], [TaxId], [ContactPerson], [CorporateIdNumber], [VatIdNumber])
                        SELECT 
                            @HeadTransactionVendorId, @CompanyId, FirstName, LastName, VendorName, AccountNumber, Email, Currency, BillCountry, BillState, BillCity, BillAddress1, BillAddress2, BillPostCode, Phone, Mobile, Fax, TollFree, Portal, '1', GETUTCDATE(), TaxId, ContactPerson, CorporateIdNumber, VatIdNumber 
                        FROM @tblVendor;
                    END
                    ELSE
                    BEGIN
                        SET @IsCustomerExist = 1;
                    END
                END

                IF @IsCustomerExist = 0
                BEGIN
                    IF @CompanyId > 0
                    BEGIN 
                        IF [dbo].[CheckValidHeadTransaction](@CompanyId, @HeadTransactionVendorId) = 1
                        BEGIN
                            SELECT @AutoNumber = [dbo].[BillCounterGet](@CompanyId);
                            SELECT 
                                @Prefix = BillNoPrefix, 
                                @Sufffix = BillNoSuffix 
                            FROM PurchaseModuleCompanySettings 
                            WHERE CompanyId = @CompanyId;

                            SET @InvoiceNo = ISNULL(@Prefix, '') + CAST(@AutoNumber AS VARCHAR) + ISNULL(@Sufffix, '');

                            INSERT INTO [dbo].[Bills]
                                ([CompanyId], [HeadTransactionVendorId], [Status], [InvoiceNo], [PoNumber], [ExchangeRate], [Price], [BillAt], [DueAt], [Currency], [Notes])
                            SELECT 
                                @CompanyId, @HeadTransactionVendorId, 0, UPPER(@InvoiceNo), PoNumber, [ExchangeRate], Price, BillAt, DueAt, Currency, Notes 
                            FROM @tblBills;

                            SET @BillId = SCOPE_IDENTITY();

                            IF @BillId > 0
                            BEGIN
                                EXECUTE [dbo].[ActivitiesAdd] 
                                    @CompanyId, @BillId, @InvoiceNo, 400, 401, @HeadTransactionVendorId, '', @UserName;
                                
						            IF EXISTS(SELECT 1 FROM PurchaseCounters WHERE CompanyId=@CompanyId)
						            BEGIN
						                UPDATE PurchaseCounters SET BillNo=@AutoNumber WHERE CompanyId=@CompanyId 
						            END
						            ELSE
						            BEGIN
						                INSERT INTO PurchaseCounters(BillNo,CompanyId) VALUES(@AutoNumber,@CompanyId)
						            END

                                DECLARE 
                                    @TempBillTranId INT,
                                    @BillTranId INT;

                                DECLARE BILL_TRN_CURSOR CURSOR FOR 
                                SELECT [Counter] FROM @tblBillTransactions;

                                OPEN BILL_TRN_CURSOR;    
                                FETCH NEXT FROM BILL_TRN_CURSOR INTO @TempBillTranId;  

                                WHILE (@@FETCH_STATUS = 0)  
                                BEGIN
                                    INSERT INTO [dbo].[BillTransactions]
                                        ([CompanyId], [BillId], [ProductId], [Description], [ProductTransactionHeadId], [Quantity], [Price])
                                    SELECT 
                                        @CompanyId, @BillId, ProductId, [Description], ProductTransactionHeadId, Quantity, Price 
                                    FROM @tblBillTransactions 
                                    WHERE [Counter] = @TempBillTranId;

                                    SET @BillTranId = SCOPE_IDENTITY();

                                    IF @BillTranId > 0
                                    BEGIN
                                        INSERT INTO [dbo].[BillTransactionTaxes]
                                            ([CompanyId], [BillTransactionId], [TransactionHeadTaxId], [Name], [Rate])
                                        SELECT 
                                            @CompanyId, @BillTranId, TransactionHeadTaxId, Name, Rate 
                                        FROM @tblBillTransactionTaxes 
                                        WHERE CounterTranId = @TempBillTranId;
                                    END

                                    FETCH NEXT FROM BILL_TRN_CURSOR INTO @TempBillTranId;  
                                END  

                                CLOSE BILL_TRN_CURSOR;   
                                DEALLOCATE BILL_TRN_CURSOR;

                                SELECT @BillId AS Id, 200 AS ErrorState, '' AS ErrorMessage, '' AS ErrorSeverity;

                                DECLARE @GuidId VARCHAR(70);
                                SELECT @GuidId = [Guid] FROM Bills WHERE Id = @BillId AND CompanyId = @CompanyId;

                                EXEC [BillsGet] @GuidId, @CompanyId;
                                COMMIT TRANSACTION;
                            END
                            ELSE
                            BEGIN
                                SELECT @BillId AS Id, 404 AS ErrorState, 'BILL TABLE ZERO RECORD' AS ErrorMessage, '' AS ErrorSeverity;
                                IF (@@TRANCOUNT > 0)
                                    ROLLBACK TRANSACTION;
                            END
                        END
                        ELSE
                        BEGIN
                            SELECT 0 AS Id, 400 AS ErrorState, 'The selected vendor is invalid!' AS ErrorMessage, '' AS ErrorSeverity;
                            IF (@@TRANCOUNT > 0)
                                ROLLBACK TRANSACTION;
                        END
                    END
                END
                ELSE
                BEGIN
                    SELECT 0 AS Id, 409 AS ErrorState, 'Vendor already exists!' AS ErrorMessage, '' AS ErrorSeverity;
                    IF (@@TRANCOUNT > 0)
                        ROLLBACK TRANSACTION;
                END
            END
            ELSE
            BEGIN
                SELECT 0 AS Id, 624 AS ErrorState, @AnyAlterMsg AS ErrorMessage, '' AS ErrorSeverity;
                IF (@@TRANCOUNT > 0)
                    ROLLBACK TRANSACTION;
            END
        END
        ELSE
        BEGIN
            SELECT 0 AS Id, 624 AS ErrorState, @AnyAlterMsg AS ErrorMessage, '' AS ErrorSeverity;
            IF (@@TRANCOUNT > 0)
                ROLLBACK TRANSACTION;
        END
    END TRY
    BEGIN CATCH
        IF (@@TRANCOUNT > 0)
            ROLLBACK TRANSACTION;

        SELECT 0 AS Id, ERROR_STATE() AS ErrorState, ERROR_MESSAGE() AS ErrorMessage, ERROR_SEVERITY() AS ErrorSeverity;
    END CATCH
END;
