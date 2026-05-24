# Exercise 1 — Bills (Accounts Payable) SP → EF Core Migration

Migration of three stored procedures from the original Saacash schema to a clean-architecture
.NET 10 / EF Core 10 solution. The SQL files in this folder are the source-of-truth originals;
each one is now annotated with `-- Step N:` markers that point to the EF code that replaces
the block.

| SP | EF Core slice | Endpoint |
|---|---|---|
| [`dbo.BillsAdd`](dbo.BillsAdd.StoredProcedure.sql) | [`Application/Bills/Create`](src/Application/Bills/Create) | `POST /bills` |
| [`dbo.BillsGet`](dbo.BillsGet.StoredProcedure.sql) | [`Application/Bills/GetByGuid`](src/Application/Bills/GetByGuid) | `GET /bills/{guid}` |
| [`dbo.BillsGetForExport`](dbo.BillsGetForExport.StoredProcedure.sql) | [`Application/Bills/GetForExport`](src/Application/Bills/GetForExport) | `POST /bills/export` |
| `dbo.ReportProfitLossGet` (bonus) | _not yet ported_ | — |

---

## Solution layout

```
billing-app.slnx
Directory.Build.props        ← net10.0, ImplicitUsings, Nullable
Directory.Packages.props     ← central package versions
docker-compose.yml           ← local SQL Server 2022

src/
  SharedKernel/              Entity, Error, Result<T>, ValidationError,
                             IDateTimeProvider, IDomainEvent(Handler)
  Domain/
    Bills/                   Bill, BillTransaction, BillTransactionTax,
                             BillPaymentDetail, RecurringBillMap,
                             BillStatus, BillFilterStatus, BillErrors,
                             BillCreatedDomainEvent
    Vendors/                 Vendor, HeadTransaction, VendorConstants
    Companies/               Company, PurchaseModuleCompanySetting,
                             PurchaseCounter, PlanTransactionType
    Products/                ProductService, HeadSub
    Activities/              Activity, ActivityTableType, ActivityActionType
  Application/
    Abstractions/            Messaging (ICommand/IQuery + handlers),
                             Behaviors (Logging + Validation decorators),
                             Data (IApplicationDbContext),
                             Services (IPlanPolicy, IBusinessClock,
                                       IBillNumberGenerator, IActivityLogger),
                             Authentication (IUserContext)
    Bills/
      Create/                CreateBillCommand + Response + Validator + Handler
      GetByGuid/             GetBillByGuidQuery + BillDetailResponse + Handler
      GetForExport/          GetBillsForExportQuery + Response + Validator + Handler
    DependencyInjection.cs   Scrutor scan + decorate, FluentValidation
  Infrastructure/
    Database/                ApplicationDbContext, DatabaseSeeder
    {Bills,Vendors,Companies,Products,Activities}/   EntityTypeConfigurations
    Services/                PlanPolicy (stub), BusinessClock (stub),
                             BillNumberGenerator, ActivityLogger
    Time/                    DateTimeProvider
    DependencyInjection.cs   UseSqlServer
  Web.Api/
    Endpoints/Bills/         Create, GetByGuid, GetForExport (IEndpoint pattern)
    Extensions/              EndpointExtensions, ResultExtensions
    Infrastructure/          CustomResults (Result → ProblemDetails)
    Program.cs               AddApplication + AddInfrastructure + AddEndpoints
                             + Swagger + Dev-only ApplyMigrationsAndSeedAsync
    appsettings*.json

tests/
  ArchitectureTests/         NetArchTest layer-dependency rules (3 tests)
  UnitTests/                 EF InMemory + handler tests
    Infrastructure/          TestDbContext, Fakes, BillsTestData
    Bills/Create/            CreateBillCommandHandlerTests (6 tests)
    Bills/GetByGuid/         GetBillByGuidQueryHandlerTests (5 tests)
    Bills/GetForExport/      GetBillsForExportQueryHandlerTests (7 tests)
```

---

## Quick start

### 1. Start SQL Server

```powershell
cd "d:\Quoc\BE Candidate 1\BE Candidate 1\Exercise 1"
docker compose down -v          # delete the SQL Server volume
docker compose up -d            # fresh DB
dotnet run --project src/Web.Api
```

Container name `bills-mssql`, port `1433`, SA password `Your_strong_Password123!`, data
persisted in the `mssql-data` named volume.

### 2. Generate the initial migration (one-off)

```powershell
dotnet tool install --global dotnet-ef        # if not already installed
dotnet ef migrations add Initial `
    --project src/Infrastructure `
    --startup-project src/Web.Api `
    -o Database/Migrations
```

### 3. Run the API

```powershell
dotnet run --project src/Web.Api
```

On startup (Development only) the seeder applies migrations and inserts baseline rows
(`Company` USD, `Vendor` "Acme Supplies", `Product` "Widget", supporting `HeadSub` /
`HeadTransactions`). It logs the generated IDs:

```
info: DatabaseSeeder[0] Baseline data ready. Use these IDs in POST /bills payloads:
       CompanyId=1, HeadTransactionVendorId=1, ProductId=1, ProductTransactionHeadId=2
```

Copy those into a Swagger `POST /bills` body and you're off. Re-runs are idempotent.

---

## SP → EF Core: step-by-step mappings

The headers of each SQL file describe the full step list. The highlights:

### `BillsAdd` → `CreateBillCommandHandler`

| SP step | EF equivalent |
|---|---|
| 2. `IsPlanAllowTransactionV2(@CompanyId, 4)` | `IPlanPolicy.CheckTransactionAllowedAsync(BillCreate)` |
| 3. `SELECT BusinessCurrency FROM Companies` | `context.Companies.Where(...).Select(c => c.BusinessCurrency)` |
| 4. multi-currency check (type 24) | `IPlanPolicy.CheckTransactionAllowedAsync(MultiCurrencyBill)` (only when bill ≠ company currency) |
| 5. `BEGIN TRANSACTION` | `await using var tx = await context.BeginTransactionAsync()` |
| 6. create vendor on the fly | `AnyAsync` + `Add HeadTransaction` + `Add Vendor` (no `SCOPE_IDENTITY`; EF re-populates `Id`) |
| 7. `CheckValidHeadTransaction(...)` | `context.HeadTransactions.AnyAsync(h => h.Id == vendorHeadId && h.CompanyId == ...)` |
| 8. `BillCounterGet` + prefix/suffix → `InvoiceNo` | `IBillNumberGenerator.NextAsync` (updates `PurchaseCounters`, reads `PurchaseModuleCompanySettings`) |
| 9. `INSERT INTO Bills` | `context.Bills.Add(...); await SaveChangesAsync()` |
| 10. `EXEC ActivitiesAdd` | `IActivityLogger.LogAsync(Bill, BillCreated)` |
| 11/12. **`CURSOR` over line items + per-row tax insert** | **`AddRange(lineItems)` + `SaveChangesAsync` + `AddRange(taxes)` + `SaveChangesAsync`** — no cursor, single batch |
| 13. `COMMIT TRANSACTION` | `await tx.CommitAsync()` |
| 14. SP returns `BillId` then `EXEC BillsGet` for full payload | Handler returns `{Id, Guid, InvoiceNo}`; clients call `GET /bills/{guid}` for the detail |

### `BillsGet` → `GetBillByGuidQueryHandler`

4 result sets collapse into a single `BillDetailResponse` (`Header` + `Lines` + `Taxes` + `Vendor`):

- Header projection computes `DueAmount = Price - Σ payments` and `IsAutoCreated` via a `RecurringBillMaps.Any(...)` in one query (replaces `LEFT JOIN` + `CASE WHEN`).
- Lines query joins `ProductServices`, `HeadTransactions`, `HeadSubs` in LINQ `join` clauses.
- Taxes query uses the materialised line-Id array instead of a sub-`SELECT`.
- Vendor query is a single `.SingleOrDefaultAsync` projection.
- Missing bill → `Result.Failure(BillErrors.NotFound(guid))` → HTTP 404.

### `BillsGetForExport` → `GetBillsForExportQueryHandler`

| SP idiom | EF equivalent |
|---|---|
| 5-branch status OR (`@Status IS NULL`, `Overdue`, `Deleted`, `Archived`, explicit) **repeated 3×** in SP | One `BuildStatusPredicate` returning `Expression<Func<Bill,bool>>`, used once |
| `@tblTaxes` table variable (per-bill `SUM(Qty*Price*Rate/100)`) | Inlined correlated `SUM` in the main projection |
| `@tblDiscounts` table variable (per-bill `SUM(Qty*Price)`) | Inlined correlated `SUM` in the main projection |
| `DATEDIFF(day, Deleted, GETUTCDATE()) <= @DeletedInvoiceDisplayFor` | Pre-computed `cutoff = UtcNow.AddDays(-N)`; `b.Deleted >= cutoff` (provider-neutral, SARGable) |
| `LIKE '%kw%'` on `InvoiceNo` / `VendorName` | `EF.Functions.Like(...)` with a left-joined `Vendor` |
| `@PageNumber` / `@RecordsPerPage` declared but unused | Implemented as `.Skip((page-1)*size).Take(size)` |
| `ORDER BY B.Id DESC` | `.OrderByDescending(b => b.Id)` |

---

## Key design choices

- **Vertical slices**: each use case is one folder under `Application/Bills/<UseCase>/` holding
  Command/Query + Validator + Handler + Response. No shared services class.
- **Result\<T\> + Error**: handlers return `Result<T>` instead of throwing for business errors.
  `Web.Api/Infrastructure/CustomResults.Problem` maps the error type to the right HTTP status
  (`Validation/Problem`=400, `NotFound`=404, `Conflict`=409, otherwise 500).
- **`internal sealed` handlers**: feature-internal by default; tests reach them via
  `<InternalsVisibleTo Include="UnitTests" />` in `Application.csproj`.
- **Stubbed cross-cutting helpers** (per the docx allowance): `IPlanPolicy` returns null
  (always allowed), `IBusinessClock` returns `UtcNow.Date`. Real implementations of
  `IBillNumberGenerator` and `IActivityLogger` are wired up.
- **Indexes** added to match the proc-level `WHERE` clauses (see entity configurations):
  - `Bills(CompanyId, Status, IsArchive, Created)`
  - `Bills(Guid, CompanyId)` unique
  - `BillTransactions(BillId, CompanyId)`
  - `BillTransactionTaxes(BillTransactionId)`
  - `BillPaymentDetails(BillId)`
  - `Vendors(CompanyId, VendorName)` unique

---

## Testing

```powershell
dotnet test billing-app.slnx
```

| Project | Count | What it covers |
|---|---|---|
| `tests/ArchitectureTests` | 3 | Layer-dependency rules (Domain ⊄ Application/Infra/Api; Application ⊄ Infra/Api; Infra ⊄ Api) |
| `tests/UnitTests/Bills/Create` | 6 | Happy path; new-vendor creation; duplicate vendor (409); plan denial; multi-currency denial; vendor-invalid |
| `tests/UnitTests/Bills/GetByGuid` | 5 | Full payload + `DueAmount`; `IsAutoCreated` flag; no payments; NotFound on bad guid / bad company |
| `tests/UnitTests/Bills/GetForExport` | 7 | Default filter excludes Deleted+Archived; computed `TotalTax`/`SubTotal`/`DueAmount`; Overdue/Archived/Deleted filters; keyword search; pagination |

Tests run against EF InMemory via `tests/UnitTests/Infrastructure/TestDbContext.cs` with the
`TransactionIgnored` warning suppressed (so `BeginTransactionAsync` is a no-op). Service
abstractions are replaced by lightweight fakes in
[`tests/UnitTests/Infrastructure/Fakes.cs`](tests/UnitTests/Infrastructure/Fakes.cs).
Baseline data comes from
[`tests/UnitTests/Infrastructure/BillsTestData.cs`](tests/UnitTests/Infrastructure/BillsTestData.cs).

---

## Outstanding / optional follow-ups

- **Bonus 4th SP** `ReportProfitLossGet` → `Application/Reports/ProfitLoss` slice, same
  vertical-slice pattern.
- **Split-query optimisation**: `GetBillByGuid` currently runs 4 sequential queries. If
  profiling shows it's too chatty, fold into a single `.AsSplitQuery()` with Includes.
- **Real `PlanPolicy` / `BusinessClock`** — currently stubbed; swap in implementations when
  the underlying tables are available.
