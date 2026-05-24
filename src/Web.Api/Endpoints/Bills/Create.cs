using Application.Abstractions.Messaging;
using Application.Bills.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Bills;

internal sealed class Create : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("bills", async (
            CreateBillCommand command,
            ICommandHandler<CreateBillCommand, CreateBillResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CreateBillResponse> result = await handler.Handle(command, cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Bills);
    }
}
