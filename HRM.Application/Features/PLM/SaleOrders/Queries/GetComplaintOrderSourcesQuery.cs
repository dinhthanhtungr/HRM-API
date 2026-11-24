using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Searching;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Enums.Deliveries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Queries;

public sealed record ComplaintSourceOrder(Guid Id, string Code, DateTime Date, string[] VaCodes);
public sealed record ComplaintSourceLine(Guid Id, Guid ProductId, string ProductName, List<ComplaintOrderVaOption> Va,
    string ProductCode, Guid FormulaId, string FormulaCode, bool HasDeliveredQuantity);
public sealed record ComplaintOrderSources(List<ComplaintSourceOrder> Orders, List<ComplaintSourceLine> Lines);
public sealed record GetComplaintOrderSourcesQuery(Guid CustomerId, Guid? OrderId, string? Keyword)
    : IRequest<ComplaintOrderSources>;

internal sealed class GetComplaintOrderSourcesQueryHandler(ISaleOrderDbContext db, ICurrentUser user)
    : IRequestHandler<GetComplaintOrderSourcesQuery, ComplaintOrderSources>
{
    public async Task<ComplaintOrderSources> Handle(GetComplaintOrderSourcesQuery request, CancellationToken ct)
    {
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(user);
        var orders = db.MerchandiseOrders.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.CustomerId == request.CustomerId && x.IsActive);
        if (request.OrderId is not { } orderId)
        {
            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                var pattern = PostgresSearchPattern.ContainsLiteral(request.Keyword.Trim());
                orders = orders.Where(x => EF.Functions.ILike(x.ExternalId, pattern, PostgresSearchPattern.EscapeCharacter));
            }
            var page = await orders.OrderByDescending(x => x.CreateDate).ThenBy(x => x.MerchandiseOrderId)
                .Take(30).Select(x => new { x.MerchandiseOrderId, x.ExternalId, x.CreateDate })
                .ToListAsync(ct);
            var orderIds = page.Select(x => x.MerchandiseOrderId).ToArray();
            var details = await db.MerchandiseOrderDetails.AsNoTracking()
                .Where(x => x.IsActive && orderIds.Contains(x.MerchandiseOrderId))
                .Select(x => new { x.MerchandiseOrderId, x.MerchandiseOrderDetailId }).ToListAsync(ct);
            var vaByDetail = await ComplaintOrderSourceReader.ReadVaByDetailAsync(
                db, companyId, details.Select(x => x.MerchandiseOrderDetailId).ToArray(), ct);
            var detailsByOrder = details.ToLookup(x => x.MerchandiseOrderId, x => x.MerchandiseOrderDetailId);
            return new(page.Select(x => new ComplaintSourceOrder(x.MerchandiseOrderId, x.ExternalId, x.CreateDate,
                detailsByOrder[x.MerchandiseOrderId].SelectMany(id => vaByDetail[id])
                    .Select(va => va.ExternalId).Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToArray())).ToList(), []);
        }

        var lines = await orders.Where(x => x.MerchandiseOrderId == orderId)
            .SelectMany(x => x.MerchandiseOrderDetails).Where(x => x.IsActive)
            .Select(x => new { x.MerchandiseOrderDetailId, x.ProductId, x.ProductNameSnapshot,
                x.ProductExternalIdSnapshot, x.FormulaId, x.FormulaExternalIdSnapshot,
                HasDeliveredQuantity = x.DeliveryOrderDetails.Any(delivery =>
                    delivery.IsActive && !delivery.IsAttach && delivery.Quantity > 0
                    && delivery.DeliveryOrder.IsActive && delivery.DeliveryOrder.CompanyId == companyId
                    && delivery.DeliveryOrder.Status != nameof(DeliveryOrderStatus.Canceled)
                    && delivery.DeliveryOrder.Status != "Cancelled") })
            .ToListAsync(ct);
        var lineVa = await ComplaintOrderSourceReader.ReadVaByDetailAsync(
            db, companyId, lines.Select(x => x.MerchandiseOrderDetailId).ToArray(), ct);
        return new([], lines.Select(line => new ComplaintSourceLine(line.MerchandiseOrderDetailId, line.ProductId,
            line.ProductNameSnapshot, lineVa[line.MerchandiseOrderDetailId], line.ProductExternalIdSnapshot,
            line.FormulaId, line.FormulaExternalIdSnapshot, line.HasDeliveredQuantity)).ToList());
    }
}
