using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;
using HRM.Application.Features.Purchasing.MaterialPlanning.Dtos;
using HRM.Application.Features.Purchasing.MaterialPlanning.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.MaterialPlanning.Queries.GetPlanningLines;

internal sealed class GetPlanningLinesQueryHandler(IPLMReadDbContext db, IWarehouseReadDbContext warehouse,
    ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetPlanningLinesQuery, OperationResult<PagedResult<PlanningLineDto>>>
{
    public async Task<OperationResult<PagedResult<PlanningLineDto>>> Handle(GetPlanningLinesQuery request, CancellationToken ct)
    {
        if (!MaterialPlanningScope.CanRead(user, permissions) ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders))
            return OperationResult<PagedResult<PlanningLineDto>>.Fail("Không có quyền hoặc thiếu công ty hiện tại.");
        var company = user.CompanyId!.Value;
        if (!await MaterialPlanningScope.Orders(db, company).AnyAsync(x => x.MerchandiseOrderId == request.OrderId, ct))
            return OperationResult<PagedResult<PlanningLineDto>>.Fail("Không tìm thấy đơn hàng trong phạm vi lập nhu cầu.");
        var query = MaterialPlanningScope.Lines(db, company).Where(x => x.MerchandiseOrderId == request.OrderId);
        var count = await query.CountAsync(ct);
        var delivered = DeliveryPlanSourceQuery.CountedAsDelivered(db.DeliveryOrderDetails.AsNoTracking(), company);
        var rows = await query.OrderBy(x => x.MerchandiseOrderDetailId)
            .Skip((int)Math.Min((long)(request.NormalizedPageNumber - 1) * request.NormalizedPageSize, int.MaxValue))
            .Take(request.NormalizedPageSize)
            .Select(x => new { Id = x.MerchandiseOrderDetailId, x.ProductId, x.ProductExternalIdSnapshot,
                x.ProductNameSnapshot, x.DeliveryRequestDate, Quantity = Math.Max(0m, x.ExpectedQuantity -
                    (delivered.Where(d => d.MerchandiseOrderDetailId == x.MerchandiseOrderDetailId)
                        .Sum(d => (decimal?)d.Quantity) ?? 0m)) }).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var productIds = rows.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && productIds.Contains(x.ProductId))
            .Select(x => new { x.ProductId, x.ColourCode, x.Name }).ToDictionaryAsync(x => x.ProductId, ct);
        var stock = await MaterialPlanningStockReader.ReadAsync(warehouse, company,
            products.Values.Select(x => x.ColourCode ?? "").ToArray(), ct);
        var links = await DeliveryPlanSourceQuery.ActiveLinks(db.MfgOrderPOs.AsNoTracking(), company)
            .Where(x => ids.Contains(x.MerchandiseOrderDetailId) && x.ProductionOrder.ProductId == x.Detail.ProductId)
            .Select(x => new { x.MerchandiseOrderDetailId, x.MfgProductionOrderId,
                x.ProductionOrder.ExternalId, x.ProductionOrder.Status }).ToListAsync(ct);
        var result = rows.Select(x =>
        {
            products.TryGetValue(x.ProductId, out var product);
            return new PlanningLineDto(x.Id, x.ProductId, product?.ColourCode ?? x.ProductExternalIdSnapshot,
                product?.Name ?? x.ProductNameSnapshot, x.Quantity,
                product is null ? 0m : MaterialPlanningStockReader.Get(stock, product.ColourCode, false), x.DeliveryRequestDate,
                links.Where(l => l.MerchandiseOrderDetailId == x.Id).OrderBy(l => l.ExternalId)
                    .Select(l => new PlanningProductionOrderDto(l.MfgProductionOrderId, l.ExternalId, l.Status)).Distinct().ToArray());
        }).ToList();
        return OperationResult<PagedResult<PlanningLineDto>>.Ok(new PagedResult<PlanningLineDto>(result, count, request.NormalizedPageNumber, request.NormalizedPageSize));
    }
}
