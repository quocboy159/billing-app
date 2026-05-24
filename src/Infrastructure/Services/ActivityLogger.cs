using Application.Abstractions.Data;
using Application.Abstractions.Services;
using Domain.Activities;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Services;

internal sealed class ActivityLogger(
    IApplicationDbContext context,
    IDateTimeProvider clock) : IActivityLogger
{
    public async Task LogAsync(
        int companyId,
        int tableTransactionId,
        string? tableTransactionValue,
        ActivityTableType tableType,
        ActivityActionType actionType,
        int transactionHeadId,
        string? remarks,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
        {
            return;
        }

        string? headName = transactionHeadId > 0
            ? await context.HeadTransactions
                .Where(h => h.Id == transactionHeadId && h.CompanyId == companyId)
                .Select(h => h.Name)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        string preparedRemarks = BuildRemarks(tableType, tableTransactionValue, actionType, headName, remarks);

        context.Activities.Add(new Activity
        {
            CompanyId = companyId,
            TableTransactionId = tableTransactionId,
            TableTransactionValue = tableTransactionValue,
            TableEnumType = tableType,
            ActionType = actionType,
            TransactionHeadName = headName,
            Remarks = preparedRemarks.Length > 500 ? preparedRemarks[..500] : preparedRemarks,
            UserName = userName,
            Created = clock.UtcNow
        });
    }

    private static string BuildRemarks(
        ActivityTableType tableType,
        string? tableTransactionValue,
        ActivityActionType actionType,
        string? transactionHeadName,
        string? remarks)
    {
        string prefix = tableType switch
        {
            ActivityTableType.Invoice => "INV",
            ActivityTableType.Estimate => "EST",
            ActivityTableType.Recurring => "REC",
            ActivityTableType.Bill => "BILL",
            ActivityTableType.RecurringBill => "REC BILL",
            ActivityTableType.PurchaseOrder => "PO",
            ActivityTableType.Banks => "BANKS",
            ActivityTableType.Reconciliation => "RECONCILIATION",
            ActivityTableType.CreditNote => "CREDITNOTE",
            _ => string.Empty
        };

        string verb = actionType switch
        {
            ActivityActionType.BillCreated => "/CREATED",
            ActivityActionType.BillUpdated => "/UPDATED",
            ActivityActionType.BillApproved => "/APPROVED",
            ActivityActionType.BillDeleteRollback => "/DELETE ROLLBACK",
            ActivityActionType.BillArchiveRollback => "/ARCHIVE ROLLBACK",
            _ => string.Empty
        };

        string value = string.IsNullOrEmpty(tableTransactionValue) ? string.Empty : "/" + tableTransactionValue;
        string head = string.IsNullOrEmpty(transactionHeadName) ? string.Empty : "/" + transactionHeadName.ToUpperInvariant();
        string rem = string.IsNullOrEmpty(remarks) ? string.Empty : "/" + remarks;

        return prefix + value + verb + head + rem;
    }
}
