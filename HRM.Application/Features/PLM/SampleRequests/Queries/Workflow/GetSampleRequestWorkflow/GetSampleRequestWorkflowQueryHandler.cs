using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestWorkflow;

internal sealed class GetSampleRequestWorkflowQueryHandler(ISender sender, IPLMReadDbContext db, IPricingVisibilityService pricingVisibility)
    : IRequestHandler<GetSampleRequestWorkflowQuery, SampleRequestWorkflowDto?>
{
    public async Task<SampleRequestWorkflowDto?> Handle(GetSampleRequestWorkflowQuery request, CancellationToken ct)
    {
        var report = await sender.Send(new GetSampleRequestDailyWorkQuery
        { SampleRequestId = request.SampleRequestId, View = SampleRequestDailyView.All, PageSize = 1 }, ct);
        var item = report.Items.SingleOrDefault();
        if (item is null) return null;
        // Chỉ lấy mã phiên bản báo giá, không lấy đơn giá/cost qua lookup này.
        var options = item.CanConfirmCustomerPrice && pricingVisibility.GetAccess().CanViewApprovedSellingPrice
            ? await db.QuotationLines.AsNoTracking().Where(x => x.SampleRequestId == item.SampleRequestId && x.IsActive &&
                x.ProductId == item.ProductId && x.Quotation.IsActive &&
                x.Quotation.CompanyId == x.SampleRequest!.CompanyId && x.Quotation.CustomerId == x.SampleRequest.CustomerId &&
                (x.Quotation.Status == QuotationStatus.Sent || x.Quotation.Status == QuotationStatus.Negotiating ||
                 x.Quotation.Status == QuotationStatus.Accepted))
                .OrderByDescending(x => x.Quotation.QuotationDate).Take(50)
                .Select(x => new SampleRequestPriceReferenceDto(x.QuotationLineId,
                    x.Quotation.ExternalId + " · v" + x.Quotation.Version + " · line " + x.SortOrder)).ToListAsync(ct)
            : [];
        return new SampleRequestWorkflowDto(item, options);
    }
}
