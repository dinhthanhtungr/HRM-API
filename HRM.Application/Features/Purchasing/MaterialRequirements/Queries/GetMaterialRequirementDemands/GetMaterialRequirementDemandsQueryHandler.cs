using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Commons.Searching;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.MaterialRequirements.Queries.GetMaterialRequirementDemands;

/// <summary>Truy nguồn nhu cầu theo lệnh; phân trang nhóm reservation trước khi tải thông tin TP/khách/đơn.</summary>
internal sealed class GetMaterialRequirementDemandsQueryHandler(
    IPLMReadDbContext production, IWarehouseReadDbContext warehouse, IPurchaseOrderDbContext purchasing,
    ICurrentUser user, ICurrentUserPermissionService permissions)
    : IRequestHandler<GetMaterialRequirementDemandsQuery, OperationResult<MaterialRequirementDemandsResponse>>
{
    public async Task<OperationResult<MaterialRequirementDemandsResponse>> Handle(GetMaterialRequirementDemandsQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !permissions.HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders) ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders) ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials))
            return OperationResult<MaterialRequirementDemandsResponse>.Fail("Bạn không có quyền xem chi tiết nhu cầu sản xuất.");
        if (user.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<MaterialRequirementDemandsResponse>.Fail("Không xác định được công ty hiện tại.");
        if (request.MaterialId == Guid.Empty || request.ProductId == Guid.Empty || request.CustomerId == Guid.Empty ||
            request.SaleOrderId == Guid.Empty || request.ProductionOrderId == Guid.Empty)
            return OperationResult<MaterialRequirementDemandsResponse>.Fail("Id không hợp lệ.");
        if (request.FromDate?.Date > request.ToDate?.Date)
            return OperationResult<MaterialRequirementDemandsResponse>.Fail("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");

        var material = await purchasing.Materials.AsNoTracking()
            .Where(x => x.MaterialId == request.MaterialId && x.CompanyId == companyId && x.IsActive == true &&
                x.ExternalId != null && x.ExternalId.Trim() != "")
            .Select(x => new { x.MaterialId, Code = x.ExternalId!.Trim().ToUpper(), Name = x.Name ?? "" })
            .FirstOrDefaultAsync(ct);
        if (material is null)
            return OperationResult<MaterialRequirementDemandsResponse>.Fail("Không tìm thấy nguyên vật liệu trong công ty.");

        var links = MaterialRequirementSaleOrderQueries.ActiveLinks(production, companyId);
        var orders = MaterialRequirementDemandQueries.EligibleOrders(
            production.MfgProductionOrders.AsNoTracking(), companyId, request.FromDate, request.ToDate);
        if (request.ProductionOrderId.HasValue)
            orders = orders.Where(x => x.MfgProductionOrderId == request.ProductionOrderId.Value);
        if (request.ProductId.HasValue)
            orders = orders.Where(x => x.ProductId == request.ProductId.Value);
        if (request.SaleOrderId.HasValue)
        {
            var matchingLinks = links.Where(x => x.SaleOrderId == request.SaleOrderId.Value);
            if (request.CustomerId.HasValue)
                matchingLinks = matchingLinks.Where(x => x.CustomerId == request.CustomerId.Value);
            orders = orders.Where(x => matchingLinks.Any(link => link.ProductionOrderId == x.MfgProductionOrderId));
        }
        else if (request.CustomerId.HasValue)
        {
            var customerId = request.CustomerId.Value;
            var validCustomer = production.Customers.AsNoTracking()
                .Where(x => x.CompanyId == companyId && x.IsActive == true && x.CustomerId == customerId);
            orders = orders.Where(x => (x.CustomerId == customerId && validCustomer.Any()) ||
                links.Any(link => link.ProductionOrderId == x.MfgProductionOrderId && link.CustomerId == customerId));
        }
        // Keyword ở chi tiết chỉ tìm mã MFG; các chiều còn lại dùng filter ID tường minh.
        orders = orders.WhereKeyword(request.Keyword, term => x =>
            EF.Functions.ILike(x.ExternalId, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
        var orderKeys = await orders.Select(x => new { x.MfgProductionOrderId, Code = x.ExternalId.Trim().ToUpper() }).ToListAsync(ct);
        if (orderKeys.GroupBy(x => x.Code).Any(g => g.Count() > 1))
            return OperationResult<MaterialRequirementDemandsResponse>.Fail("Có lệnh sản xuất trùng mã; không thể xác định nguồn giữ chỗ.");
        var orderCodes = orderKeys.Select(x => x.Code).ToArray();
        var demand = MaterialRequirementDemandQueries.OpenDemand(warehouse.WarehouseTempStocks.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Code.Trim().ToUpper() == material.Code &&
                orderCodes.Contains(x.VaCode.Trim().ToUpper())));
        var count = await demand.CountAsync(ct);
        var total = await demand.SumAsync(x => (decimal?)x.Quantity, ct) ?? 0m;
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var page = await demand.OrderBy(x => x.VaCode)
            .Skip((int)Math.Min((long)(pageNumber - 1) * pageSize, int.MaxValue)).Take(pageSize).ToListAsync(ct);
        var pageCodes = page.Select(x => x.VaCode).ToHashSet(StringComparer.Ordinal);
        var pageIds = orderKeys.Where(x => pageCodes.Contains(x.Code)).Select(x => x.MfgProductionOrderId).ToArray();

        var headers = await orders.Where(x => pageIds.Contains(x.MfgProductionOrderId))
            .Select(x => new { x.MfgProductionOrderId, x.ExternalId, x.Status, x.ProductId, x.CustomerId, x.RequiredDate })
            .ToListAsync(ct);
        var productIds = headers.Select(x => x.ProductId).ToArray();
        var products = await production.Products.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive && productIds.Contains(x.ProductId))
            .Select(x => new { x.ProductId, x.ColourCode, x.Name }).ToDictionaryAsync(x => x.ProductId, ct);
        var customerIds = headers.Where(x => x.CustomerId.HasValue).Select(x => x.CustomerId!.Value).ToArray();
        var customers = await production.Customers.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive == true && customerIds.Contains(x.CustomerId))
            .Select(x => new { x.CustomerId, x.ExternalId, x.CustomerName }).ToDictionaryAsync(x => x.CustomerId, ct);
        var saleRows = await links.Where(x => pageIds.Contains(x.ProductionOrderId)).ToListAsync(ct);
        var saleMap = saleRows.GroupBy(x => x.ProductionOrderId).ToDictionary(g => g.Key, g => g
            .DistinctBy(x => x.SaleOrderDetailId).OrderBy(x => x.SaleOrderCode, StringComparer.Ordinal).ThenBy(x => x.SaleOrderDetailId)
            .Select(x => new MaterialRequirementSaleOrderDto(x.SaleOrderId, x.SaleOrderCode, x.SaleOrderDetailId,
                x.CustomerId, x.CustomerCode, x.CustomerName)).ToArray());
        var headerMap = headers.ToDictionary(x => x.ExternalId.Trim().ToUpper());
        var items = page.Select(row =>
        {
            var order = headerMap[row.VaCode];
            products.TryGetValue(order.ProductId, out var product);
            var customer = order.CustomerId.HasValue ? customers.GetValueOrDefault(order.CustomerId.Value) : null;
            return new MaterialRequirementDemandDto(order.MfgProductionOrderId, order.ExternalId, order.Status,
                product?.ProductId, product?.ColourCode, product?.Name,
                customer?.CustomerId, customer?.ExternalId, customer?.CustomerName, order.RequiredDate.Date,
                row.RequestedQuantityKg, row.UsedQuantityKg, row.Quantity,
                saleMap.GetValueOrDefault(order.MfgProductionOrderId) ?? []);
        }).ToList();
        return OperationResult<MaterialRequirementDemandsResponse>.Ok(new MaterialRequirementDemandsResponse(
            material.MaterialId, material.Code, material.Name, total,
            new PagedResult<MaterialRequirementDemandDto>(items, count, pageNumber, pageSize)));
    }
}
