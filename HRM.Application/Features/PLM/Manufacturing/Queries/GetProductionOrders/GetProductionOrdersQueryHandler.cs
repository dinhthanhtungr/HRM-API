using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrders;

internal sealed class GetProductionOrdersQueryHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions
) : IRequestHandler<GetProductionOrdersQuery, OperationResult<PagedResult<GetSummaryMfgProductionOrder>>>
{
    public async Task<OperationResult<PagedResult<GetSummaryMfgProductionOrder>>> Handle(GetProductionOrdersQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null)
        {
            return OperationResult<PagedResult<GetSummaryMfgProductionOrder>>.Fail(error);
        }

        if (request.From > request.To)
        {
            return OperationResult<PagedResult<GetSummaryMfgProductionOrder>>.Fail("Khoảng ngày không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var page = Math.Max(1, request.PageNumber);
        var size = request.PageSize <= 0 ? 15 : Math.Min(100, request.PageSize);
        if ((long)(page - 1) * size > int.MaxValue)
        {
            return OperationResult<PagedResult<GetSummaryMfgProductionOrder>>.Fail("Trang không hợp lệ.");
        }

        var orders = db.MfgProductionOrders.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive);
        var links = db.MfgOrderPOs.AsNoTracking().Where(x => x.Detail.MerchandiseOrder.CompanyId == company && x.ProductionOrder.CompanyId == company);
        var selections = db.ProductionSelectVersions.AsNoTracking().Where(x => x.CompanyId == company);
        var formulas = db.ManufacturingFormulas.AsNoTracking().Where(x => x.CompanyId == company);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var pattern = $"%{request.Keyword.Trim()}%";
            orders = orders.Where(o => EF.Functions.ILike(o.ExternalId, pattern)
                || (o.ProductExternalIdSnapshot != null && EF.Functions.ILike(o.ProductExternalIdSnapshot, pattern))
                || (o.ProductNameSnapshot != null && EF.Functions.ILike(o.ProductNameSnapshot, pattern))
                || (o.ColorName != null && EF.Functions.ILike(o.ColorName, pattern))
                || (o.CustomerExternalIdSnapshot != null && EF.Functions.ILike(o.CustomerExternalIdSnapshot, pattern))
                || (o.CustomerNameSnapshot != null && EF.Functions.ILike(o.CustomerNameSnapshot, pattern))
                || (o.FormulaExternalIdSnapshot != null && EF.Functions.ILike(o.FormulaExternalIdSnapshot, pattern))
                || o.Product.SampleRequests.Any(sr => sr.IsActive && sr.CompanyId == company && EF.Functions.ILike(sr.ExternalId, pattern))
                || db.Formulas.Any(f => f.IsActive && f.CompanyId == company && f.ProductId == o.ProductId
                && EF.Functions.ILike(f.ExternalId, pattern))
                || (
                from s in selections
                join f in formulas on s.ManufacturingFormulaId equals f.ManufacturingFormulaId
                where s.MfgProductionOrderId == o.MfgProductionOrderId
                select f.ExternalId).Any(code => EF.Functions.ILike(code, pattern))
                || links.Any(l => l.MfgProductionOrderId == o.MfgProductionOrderId
                && EF.Functions.ILike(l.Detail.MerchandiseOrder.ExternalId, pattern)));
        }

        if (request.MfgProductionOrderId is { } id && id != Guid.Empty)
        {
            orders = orders.Where(x => x.MfgProductionOrderId == id);
        }

        if (request.Statuses is { Count: > 0 })
        {
            orders = orders.Where(x => request.Statuses.Contains(x.Status));
        }

        if (request.From.HasValue)
        {
            orders = orders.Where(x => x.CreatedDate >= request.From);
        }

        if (request.To.HasValue)
        {
            orders = orders.Where(x => x.CreatedDate <= request.To);
        }

        var count = await orders.CountAsync(ct);
        var sorted = request.IsLastUpdate ? orders.OrderByDescending(x => x.UpdatedDate).ThenByDescending(x => x.CreatedDate) : orders.OrderByDescending(x => x.CreatedDate);
        var items = await sorted.Skip((page - 1) * size).Take(size).Select(o => new GetSummaryMfgProductionOrder
        {
            MfgProductionOrderId = o.MfgProductionOrderId,
            ExternalId = o.ExternalId,
            MfgFormualaExternalIdSnapshot = (
            from s in selections
            join f in formulas on s.ManufacturingFormulaId equals f.ManufacturingFormulaId
            where s.MfgProductionOrderId == o.MfgProductionOrderId && s.ValidTo == null
            orderby s.ValidFrom descending
            select f.ExternalId).FirstOrDefault(),
            MerchandiseOrderId = links.Where(l => l.MfgProductionOrderId == o.MfgProductionOrderId).OrderByDescending(l => l.Detail.MerchandiseOrderId)
                .Select(l => l.Detail.MerchandiseOrderId)
                .FirstOrDefault(),
            MerchandiseOrderExternalId = links.Where(l => l.MfgProductionOrderId == o.MfgProductionOrderId).OrderByDescending(l => l.Detail.MerchandiseOrderId)
                .Select(l => l.Detail.MerchandiseOrder.ExternalId)
                .FirstOrDefault(),
            ProductNameSnapshot = o.ProductNameSnapshot,
            ProductExternalIdSnapshot = o.ProductExternalIdSnapshot,
            CustomerNameSnapshot = o.CustomerNameSnapshot,
            CustomerExternalIdSnapshot = o.CustomerExternalIdSnapshot,
            RequestedQuantity = o.TotalQuantityRequest,
            TotalQuantity = o.TotalQuantity,
            Status = o.Status,
            IsPrintedStock = o.IsPrintedStock,
            CheckedDate = o.CheckedDate,
            CheckName = o.CheckerNavigation != null && o.CheckerNavigation.CompanyId == company ? o.CheckerNavigation.FullName ?? "" : "",
            CreatedDate = o.CreatedDate,
            BagType = o.BagType
        })
            .ToListAsync(ct);
        await ManufacturingFormulaHistoryReader.FillAsync(db, company, items, ct);
        return OperationResult<PagedResult<GetSummaryMfgProductionOrder>>.Ok(new PagedResult<GetSummaryMfgProductionOrder>(items, count, page, size));
    }
}
