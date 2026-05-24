using Application.Abstractions.Messaging;
using Application.Bills.GetByGuid;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Bills;

internal sealed class GetByGuid : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("bills/{guid:guid}", async (
            Guid guid,
            int companyId,
            IQueryHandler<GetBillByGuidQuery, BillDetailResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<BillDetailResponse> result = await handler.Handle(
                new GetBillByGuidQuery { Guid = guid, CompanyId = companyId },
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Bills);
    }
}
