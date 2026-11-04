using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetAvailableLots;

internal sealed class GetAvailableDeliveryLotsQueryHandler
    : IRequestHandler<GetAvailableDeliveryLotsQuery, OperationResult<IReadOnlyList<AvailableDeliveryLotDto>>>
{
    private readonly IDispatchReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentUserPermissionService _permissionService;
    private readonly DeliveryOrderLotInventoryService _inventoryService;

    public GetAvailableDeliveryLotsQueryHandler(
        IDispatchReadDbContext dbContext,
        ICurrentUser currentUser,
        ICurrentUserPermissionService permissionService,
        DeliveryOrderLotInventoryService inventoryService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _inventoryService = inventoryService;
    }

    public async Task<OperationResult<IReadOnlyList<AvailableDeliveryLotDto>>> Handle(
        GetAvailableDeliveryLotsQuery request,
        CancellationToken cancellationToken)
    {
        if (!DeliveryOrderAccessRules.CanRead(_currentUser) ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<IReadOnlyList<AvailableDeliveryLotDto>>.Fail("Bạn không có quyền xem lot giao hàng.");
        }

        if (request.MerchandiseOrderDetailId == Guid.Empty || request.ProductId == Guid.Empty)
        {
            return OperationResult<IReadOnlyList<AvailableDeliveryLotDto>>.Fail("ProductId hoặc dòng PO không hợp lệ.");
        }

        var line = await _dbContext.MerchandiseOrderDetails
            .AsNoTracking()
            .Where(x =>
                x.MerchandiseOrderDetailId == request.MerchandiseOrderDetailId &&
                x.ProductId == request.ProductId &&
                x.IsActive &&
                x.MerchandiseOrder.IsActive &&
                x.MerchandiseOrder.CompanyId == companyId)
            .Select(x => new { x.ProductId, ProductCode = x.ProductExternalIdSnapshot, BagWeightKg = x.Product.IsActive && x.Product.CompanyId == companyId
                    ? x.Product.SampleRequests.Where(sr => sr.CompanyId == companyId && sr.IsActive &&
                        sr.CustomerId == x.MerchandiseOrder.CustomerId)
                        .OrderByDescending(sr => sr.CreatedDate).ThenByDescending(sr => sr.SampleRequestId)
                        .Select(sr => sr.BagWeight > 0 ? (int?)sr.BagWeight : null).FirstOrDefault()
                    : null })
            .FirstOrDefaultAsync(cancellationToken);

        if (line is null)
        {
            return OperationResult<IReadOnlyList<AvailableDeliveryLotDto>>.Fail("Không tìm thấy product/dòng PO trong công ty hiện tại.");
        }

        var canViewCost = DeliveryOrderCostVisibilityRules.CanViewCost(_permissionService);
        var inventory = await _inventoryService.LoadAsync(
            companyId,
            [(line.ProductId, line.ProductCode)],
            includeCost: canViewCost,
            cancellationToken: cancellationToken);
        var lotCodes = inventory.Values.Select(x => DeliveryOrderLotInventoryRules.Normalize(x.LotNo)).Distinct().ToArray();
        var lotOrders = await _dbContext.MfgOrderPOs.AsNoTracking()
            .Where(x => x.IsActive && x.Detail.IsActive && x.Detail.ProductId == request.ProductId &&
                x.Detail.MerchandiseOrder.IsActive && x.Detail.MerchandiseOrder.CompanyId == companyId &&
                x.ProductionOrder.IsActive && x.ProductionOrder.CompanyId == companyId)
            .SelectMany(x => x.ProductionOrder.ProductionSelectVersions
                .Where(v => v.ValidTo == null && v.CompanyId == companyId && v.ManufacturingFormula != null &&
                    v.ManufacturingFormula.IsActive && v.ManufacturingFormula.CompanyId == companyId &&
                    lotCodes.Contains(v.ManufacturingFormula.ExternalId.Trim().ToUpper()))
                .Select(v => new { LotNo = v.ManufacturingFormula!.ExternalId,
                    OrderCode = x.Detail.MerchandiseOrder.ExternalId }))
            .Distinct().ToListAsync(cancellationToken);
        var ordersByLot = lotOrders.GroupBy(x => DeliveryOrderLotInventoryRules.Normalize(x.LotNo))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.OrderCode)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
        var result = inventory.Values
            .Where(x => x.AvailableQuantity > 0m && x.ProductAvailableQuantity > 0m)
            .OrderBy(x => x.LotNo)
            .Select(x => new AvailableDeliveryLotDto
            {
                BagWeightKg = line.BagWeightKg,
                LotNo = x.LotNo,
                OrderExternalIds = ordersByLot.GetValueOrDefault(DeliveryOrderLotInventoryRules.Normalize(x.LotNo)) ?? [],
                StockType = x.StockType.ToString(),
                CanSelect = true,
                OnHandQuantity = x.OnHandQuantity,
                ReservedQuantity = x.ReservedQuantity,
                AvailableQuantity = Math.Min(x.AvailableQuantity, x.ProductAvailableQuantity),
                UnitCostSnapshot = canViewCost ? x.UnitCostSnapshot : null
            })
            .ToList();

        return OperationResult<IReadOnlyList<AvailableDeliveryLotDto>>.Ok(result);
    }
}
