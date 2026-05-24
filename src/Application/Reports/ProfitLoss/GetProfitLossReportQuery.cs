using Application.Abstractions.Messaging;

namespace Application.Reports.ProfitLoss;

public sealed class GetProfitLossReportQuery : IQuery<ProfitLossReportResponse>
{
    public int CompanyId { get; set; }
    public DateTime StartDate1 { get; set; }
    public DateTime EndDate1 { get; set; }
    public DateTime? StartDate2 { get; set; }
    public DateTime? EndDate2 { get; set; }

    /// <summary>
    /// When true the StartDate lower bound is ignored (retained-earnings calculation).
    /// Maps to the SP's @IsRetain bit parameter.
    /// </summary>
    public bool IsRetain { get; set; }
}
