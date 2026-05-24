namespace Domain.Companies;

/// <summary>
/// The 24 feature limits enforced by <c>dbo.IsPlanAllowBulkTransactionsV2</c>.
/// Values match the SP's @FeatureId constants exactly.
/// </summary>
public enum PlanFeature
{
    Invoice = 1,
    EstimateOrRecurringInvoice = 2,
    Product = 3,
    Bill = 4,
    EstimateBill = 5,
    Vendor = 6,
    BankAccount = 7,
    BankReconciliation = 8,
    HeadTransaction = 9,
    InvoiceAlt = 10,
    UnlimitedFeature11 = 11,
    HeadTransactionSub2004 = 12,
    UnlimitedFeature13 = 13,
    UnlimitedFeature14 = 14,
    UnlimitedFeature15 = 15,
    CreditNote = 16,
    UnlimitedFeature17 = 17,
    OwnerUserRole = 18,
    Customer = 19,
    User = 20,
    Expense = 21,
    RecurringBill = 22,
    MultiCurrencyInvoice = 23,
    MultiCurrencyBill = 24
}

/// <summary>
/// Sentinel limit values used by the plan-policy resolution.
/// </summary>
public static class PlanFeatureLimits
{
    /// <summary>The SP's @Unlimited constant (= 10000). Any count below this passes unconditionally.</summary>
    public const int Unlimited = 10000;

    /// <summary>"Allow with no limit" — distinct from Unlimited but treated identically by the policy.</summary>
    public const int NoLimit = 0;

    /// <summary>Feature not enabled for this company (no active subscription, no free plan match).</summary>
    public const int Unavailable = -1;
}
