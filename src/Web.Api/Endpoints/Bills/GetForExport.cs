using Application.Abstractions.Messaging;
using Application.Bills.GetForExport;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Bills;

internal sealed class GetForExport : IEndpoint
{
    public sealed class Request
    {
        public int CompanyId { get; set; }
        public int? VendorId { get; set; }
        public int? Status { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? InvoiceNo { get; set; }
        public string? FilterKeyword { get; set; }
        public int PageNumber { get; set; } = 1;
        public int RecordsPerPage { get; set; } = 20;
        public int DeletedInvoiceDisplayFor { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("bills/export", async (
            Request request,
            IQueryHandler<GetBillsForExportQuery, List<BillExportRowResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetBillsForExportQuery
            {
                CompanyId = request.CompanyId,
                VendorId = request.VendorId,
                Status = request.Status.HasValue ? BillExportStatusFilter.FromInt(request.Status.Value) : null,
                From = request.From,
                To = request.To,
                InvoiceNo = request.InvoiceNo,
                FilterKeyword = request.FilterKeyword,
                PageNumber = request.PageNumber,
                RecordsPerPage = request.RecordsPerPage,
                DeletedInvoiceDisplayFor = request.DeletedInvoiceDisplayFor
            };

            Result<List<BillExportRowResponse>> result = await handler.Handle(query, cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Bills);
    }
}
