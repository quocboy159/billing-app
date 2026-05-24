/* =============================================================================================
   EF Core port :  src/Application/Bills/Create/CreateBillCommandHandler.cs
   Endpoint     :  POST /bills   (src/Web.Api/Endpoints/Bills/Create.cs)
   Command DTO  :  CreateBillCommand                 -- mirrors the 5 TVPs/params below
   Unit tests   :  tests/UnitTests/Bills/Create/CreateBillCommandHandlerTests.cs

   Step-by-step mapping (each step is also commented inline below):
     1. Read header inputs                  -> taken from CreateBillCommand.Bill (DTO)
     2. Plan check (type=4)                 -> IPlanPolicy.CheckTransactionAllowedAsync(..., BillCreate)
     3. Read company currency               -> context.Companies.Where(...).Select(...).SingleOrDefaultAsync
     4. Multi-currency plan check (type=24) -> IPlanPolicy.CheckTransactionAllowedAsync(..., MultiCurrencyBill)
     5. Open transaction                    -> context.BeginTransactionAsync()
     6. Create vendor on the fly            -> context.Vendors.AnyAsync + Add HeadTransaction + Add Vendor
     7. Validate vendor belongs to company  -> context.HeadTransactions.AnyAsync(...)
     8. Generate next InvoiceNo             -> IBillNumberGenerator.NextAsync(...) [bumps PurchaseCounters,
                                              reads Prefix/Suffix from PurchaseModuleCompanySettings]
     9. Insert Bills row (Status=Draft)     -> context.Bills.Add + SaveChangesAsync (captures Id)
    10. Log activity                        -> IActivityLogger.LogAsync(Bill=400, Action=BillCreated=401)
    11. Insert BillTransactions             -> context.BillTransactions.AddRange + SaveChangesAsync
        (NO CURSOR -- EF tracks generated Ids so a single AddRange + one SaveChanges replaces the loop)
    12. Insert BillTransactionTaxes         -> context.BillTransactionTaxes.AddRange + SaveChangesAsync
    13. Commit                              -> transaction.CommitAsync()
    14. Return full payload                 -> handler returns { Id, Guid, InvoiceNo }; client calls
                                              GET /bills/{guid} for the BillsGet equivalent
    Error paths                             -> Result.Failure<>(BillErrors.XYZ); ProblemDetails via
                                              Web.Api/Infrastructure/CustomResults.cs
   ============================================================================================= */
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
    -- Step 5 (SP-side): open the ambient transaction.
    -- EF equivalent: `await using IDbContextTransaction transaction = await context.BeginTransactionAsync(...)`
    BEGIN TRANSACTION;

    BEGIN TRY
        -- Step 1: read header inputs from the TVP.
        -- EF equivalent: command.Bill (BillHeaderInput DTO)
        SELECT
            @CompanyId = CompanyId,
            @HeadTransactionVendorId = HeadTransactionVendorId,
            @VendorCurrency = Currency
        FROM @tblBills;

        -- Step 2: plan check for "create bill" (transactionType = 4 == PlanTransactionType.BillCreate).
        -- EF equivalent: IPlanPolicy.CheckTransactionAllowedAsync(companyId, BillCreate)
        SET @AnyAlterMsg = [dbo].[IsPlanAllowTransactionV2](@CompanyId, 4, 0, '');

        IF @AnyAlterMsg IS NULL
        BEGIN
            -- Step 3: read the company's base currency.
            -- EF equivalent: context.Companies.AsNoTracking()
            --                       .Where(c => c.Id == header.CompanyId)
            --                       .Select(c => c.BusinessCurrency)
            --                       .SingleOrDefaultAsync(...)
            SELECT @CompanyCurrency = BusinessCurrency
            FROM Companies
            WHERE Id = @CompanyId;

            -- Step 4: plan check for multi-currency bills (transactionType = 24 == MultiCurrencyBill).
            -- EF equivalent: IPlanPolicy.CheckTransactionAllowedAsync(companyId, MultiCurrencyBill)
            --                only invoked when bill currency differs from company currency.
            SET @AnyAlterMsg = [dbo].[IsPlanAllowTransactionV2](@CompanyId, 24, 0, '');

            IF @VendorCurrency IS NULL
                OR @CompanyCurrency = @VendorCurrency
                OR (@CompanyCurrency != @VendorCurrency AND @AnyAlterMsg IS NULL)
            BEGIN
                -- Step 6: create vendor on the fly when caller passed HeadTransactionVendorId = 0
                -- and supplied a vendor row in @tblVendor.
                -- EF equivalent: see the `if (vendorHeadId == 0 && command.Vendor is not null)` block
                -- in CreateBillCommandHandler.cs
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

                    -- Step 6a: vendor uniqueness check (per company).
                    -- EF equivalent: context.Vendors.AsNoTracking().AnyAsync(v => v.CompanyId == ... && v.VendorName == ...)
                    -- On true -> Result.Failure<>(BillErrors.VendorAlreadyExists())  -> HTTP 409 Conflict
                    IF NOT EXISTS(SELECT 1 FROM Vendors WHERE CompanyId = @CompanyId AND VendorName = @VendorName)
                    BEGIN
                        -- Step 6b: insert the vendor's HeadTransaction row.
                        -- SubHeadsId = 2003 is encoded as Domain/Vendors/VendorConstants.VendorSubHeadId.
                        -- SCOPE_IDENTITY() is replaced by EF tracking: after SaveChangesAsync,
                        -- `headTransaction.Id` is populated automatically.
                        INSERT INTO HeadTransactions
                            ([CompanyId], [SubHeadsId], [AccountTaxId], [Name], [Currency])
                        VALUES
                            (@CompanyId, 2003, @AccountNumber, @VendorName, @Currency);

                        SET @HeadTransactionVendorId = SCOPE_IDENTITY();

                        -- Step 6c: insert the Vendor row, linked back to the HeadTransaction we just made.
                        -- EF equivalent: context.Vendors.Add(new Vendor { HeadTransactionId = headTransaction.Id, ... })
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
                        -- Step 7: validate the vendor HeadTransaction belongs to this company.
                        -- EF equivalent: context.HeadTransactions.AsNoTracking()
                        --                       .AnyAsync(h => h.Id == vendorHeadId && h.CompanyId == header.CompanyId)
                        -- On false -> Result.Failure<>(BillErrors.VendorInvalid())  -> HTTP 400.
                        IF [dbo].[CheckValidHeadTransaction](@CompanyId, @HeadTransactionVendorId) = 1
                        BEGIN
                            -- Step 8: generate the next invoice number (counter + prefix/suffix).
                            -- EF equivalent: IBillNumberGenerator.NextAsync(companyId), implemented in
                            --                Infrastructure/Services/BillNumberGenerator.cs -- it reads
                            --                PurchaseCounters + PurchaseModuleCompanySettings and bumps the counter.
                            SELECT @AutoNumber = [dbo].[BillCounterGet](@CompanyId);
                            SELECT
                                @Prefix = BillNoPrefix,
                                @Sufffix = BillNoSuffix
                            FROM PurchaseModuleCompanySettings
                            WHERE CompanyId = @CompanyId;

                            SET @InvoiceNo = ISNULL(@Prefix, '') + CAST(@AutoNumber AS VARCHAR) + ISNULL(@Sufffix, '');

                            -- Step 9: insert the Bills row (status 0 == BillStatus.Draft).
                            -- EF equivalent: context.Bills.Add(new Bill { ... }); await context.SaveChangesAsync();
                            --                After SaveChanges, bill.Id is populated (replaces SCOPE_IDENTITY()).
                            INSERT INTO [dbo].[Bills]
                                ([CompanyId], [HeadTransactionVendorId], [Status], [InvoiceNo], [PoNumber], [ExchangeRate], [Price], [BillAt], [DueAt], [Currency], [Notes])
                            SELECT
                                @CompanyId, @HeadTransactionVendorId, 0, UPPER(@InvoiceNo), PoNumber, [ExchangeRate], Price, BillAt, DueAt, Currency, Notes
                            FROM @tblBills;

                            SET @BillId = SCOPE_IDENTITY();

                            IF @BillId > 0
                            BEGIN
                                -- Step 10: log activity (TableType 400 == Bill, Action 401 == BillCreated).
                                -- EF equivalent: IActivityLogger.LogAsync(companyId, billId, invoiceNo,
                                --                ActivityTableType.Bill, ActivityActionType.BillCreated, ...)
                                EXECUTE [dbo].[ActivitiesAdd]
                                    @CompanyId, @BillId, @InvoiceNo, 400, 401, @HeadTransactionVendorId, '', @UserName;
                                
                                -- Step 8b: persist the bumped counter (upsert PurchaseCounters).
                                -- EF equivalent: handled inside IBillNumberGenerator.NextAsync()
                                -- (Infrastructure/Services/BillNumberGenerator.cs) which Add()s a new row
                                -- if none exists or mutates the tracked entity in place.
						            IF EXISTS(SELECT 1 FROM PurchaseCounters WHERE CompanyId=@CompanyId)
						            BEGIN
						                UPDATE PurchaseCounters SET BillNo=@AutoNumber WHERE CompanyId=@CompanyId
						            END
						            ELSE
						            BEGIN
						                INSERT INTO PurchaseCounters(BillNo,CompanyId) VALUES(@AutoNumber,@CompanyId)
						            END

                                -- Step 11/12: insert all line items + their taxes.
                                -- The SP walks a CURSOR so it can capture the new BillTransactions.Id
                                -- via SCOPE_IDENTITY() and use it to FK each tax row.
                                -- EF equivalent (no cursor, single batch):
                                --     // Step 11
                                --     var tranByCounter = command.Transactions.ToDictionary(t => t.Counter,
                                --         t => new BillTransaction { CompanyId = ..., BillId = bill.Id, ... });
                                --     context.BillTransactions.AddRange(tranByCounter.Values);
                                --     await context.SaveChangesAsync(); // now every tran has its Id
                                --
                                --     // Step 12
                                --     var taxes = command.Taxes.Select(t => new BillTransactionTax {
                                --         BillTransactionId = tranByCounter[t.CounterTranId].Id, ... });
                                --     context.BillTransactionTaxes.AddRange(taxes);
                                --     await context.SaveChangesAsync();
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

                                -- Step 14: SP returns BillId + status 200 first, then re-runs BillsGet
                                -- so the client receives the full payload in one round-trip.
                                -- EF equivalent: handler returns just { Id, Guid, InvoiceNo }. Callers
                                -- fetch the full detail with GET /bills/{guid} (one extra HTTP call but
                                -- the same SQL cost; keeps the command's response narrow).
                                SELECT @BillId AS Id, 200 AS ErrorState, '' AS ErrorMessage, '' AS ErrorSeverity;

                                DECLARE @GuidId VARCHAR(70);
                                SELECT @GuidId = [Guid] FROM Bills WHERE Id = @BillId AND CompanyId = @CompanyId;

                                EXEC [BillsGet] @GuidId, @CompanyId;
                                -- Step 13: commit. EF equivalent: await transaction.CommitAsync(ct);
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
        -- Error path. EF equivalent:
        --   - validation/business errors -> Result.Failure<>(BillErrors.X)
        --   - the `await using IDbContextTransaction transaction` auto-disposes (rollback)
        --     if CommitAsync is never reached
        --   - Web.Api/Infrastructure/CustomResults.Problem maps the Result.Error to
        --     a ProblemDetails payload with the right HTTP status code.
        IF (@@TRANCOUNT > 0)
            ROLLBACK TRANSACTION;

        SELECT 0 AS Id, ERROR_STATE() AS ErrorState, ERROR_MESSAGE() AS ErrorMessage, ERROR_SEVERITY() AS ErrorSeverity;
    END CATCH
END;
