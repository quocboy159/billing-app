using Application.Abstractions.Messaging;
using Application.Reports.ProfitLoss;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reports;

internal sealed class GetProfitLoss : IEndpoint
{
    public sealed class Request
    {
        public int CompanyId { get; set; }
        public DateTime StartDate1 { get; set; }
        public DateTime EndDate1 { get; set; }
        public DateTime? StartDate2 { get; set; }
        public DateTime? EndDate2 { get; set; }
        public bool IsRetain { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("reports/profit-loss", async (
            Request request,
            IQueryHandler<GetProfitLossReportQuery, ProfitLossReportResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetProfitLossReportQuery
            {
                CompanyId = request.CompanyId,
                StartDate1 = request.StartDate1,
                EndDate1 = request.EndDate1,
                StartDate2 = request.StartDate2,
                EndDate2 = request.EndDate2,
                IsRetain = request.IsRetain
            };

            Result<ProfitLossReportResponse> result = await handler.Handle(query, cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Reports);
    }
}
