using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestLookup;
using HRM.Domain.Enums.Merchadises;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

internal sealed class GetCustomerSharedSamplesQueryHandler(ICurrentUser user, CustomerSampleSharingService sharing, ISender sender, ICustomerSampleSharingDbContext db)
    : IRequestHandler<GetCustomerSharedSamplesQuery, OperationResult<IReadOnlyList<SharedCustomerSampleDto>>>
{
    public async Task<OperationResult<IReadOnlyList<SharedCustomerSampleDto>>> Handle(GetCustomerSharedSamplesQuery request, CancellationToken ct)
    {
        if (!sharing.Enabled || !user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty
            || request.CustomerId == Guid.Empty || request.Keyword?.Length > 200
            || await sharing.ResolveOwnerIdsAsync(company, request.CustomerId, ct) is null)
            return OperationResult<IReadOnlyList<SharedCustomerSampleDto>>.Fail("Không thể xem mẫu của khách này.");
        // Keep eligibility independent: both lookups resolve the group, while
        // each document keeps its own status/formula and write validation.
        var target = await db.Customers.AsNoTracking().Where(x => x.CompanyId == company && x.CustomerId == request.CustomerId && x.IsActive == true)
            .Select(x => new { x.ExternalId, x.CustomerName }).SingleAsync(ct);
        var orders = await sender.Send(new GetSampleRequestLookupQuery { CustomerId = request.CustomerId, ForSaleOrder = true,
            OrderType = OrderType.Merchandise, Keyword = request.Keyword, Take = 50, IsActive = true }, ct);
        var quotes = await sender.Send(new GetSampleRequestLookupQuery { CustomerId = request.CustomerId,
            OrderType = OrderType.Merchandise, Keyword = request.Keyword, Take = 50, IsActive = true }, ct);
        var orderIds = orders.Where(x => x.FormulaId.HasValue).Select(x => x.SampleRequestId).ToHashSet();
        var quoteIds = quotes.Select(x => x.SampleRequestId).ToHashSet();
        var result = orders.Concat(quotes).DistinctBy(x => x.SampleRequestId).Select(x => new SharedCustomerSampleDto(
            x.SampleRequestId, x.ExternalId, x.ProductId, x.ProductCode, x.ProductName, x.CustomerId, x.CustomerExternalId, x.CustomerName,
            orderIds.Contains(x.SampleRequestId), quoteIds.Contains(x.SampleRequestId),
            orderIds.Contains(x.SampleRequestId) ? null : "sampleNotReady",
            quoteIds.Contains(x.SampleRequestId) ? null : "quotationCustomerMismatch", target.ExternalId ?? "", target.CustomerName ?? "")).ToArray();
        return OperationResult<IReadOnlyList<SharedCustomerSampleDto>>.Ok(result);
    }
}
