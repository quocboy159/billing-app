namespace Application.Reports.ProfitLoss;

public sealed class ProfitLossReportResponse
{
    public List<ProfitLossRow> Period1 { get; init; } = [];
    public List<ProfitLossRow> Period2 { get; init; } = [];
}

/// <summary>
/// One row per (Source, SubHead, TransactionHead) bucket -- mirrors the columns the SP returns.
/// </summary>
public sealed class ProfitLossRow
{
    public ProfitLossRowType Type { get; init; }
    public int HeadId { get; init; }
    public int SubHeadId { get; init; }
    public string SubHeadName { get; init; } = string.Empty;
    public int TransactionId { get; init; }
    public string TransactionName { get; init; } = string.Empty;
    public decimal ProductPrice { get; init; }
    public int OrderId { get; init; }
}

public enum ProfitLossRowType
{
    Invoice = 1,
    Bill = 2
}
