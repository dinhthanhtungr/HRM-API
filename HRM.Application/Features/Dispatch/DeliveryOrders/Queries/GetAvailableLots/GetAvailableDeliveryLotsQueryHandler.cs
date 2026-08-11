using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetAvailableLots;

internal sealed class GetAvailableDeliveryLotsQueryHandler
    : IRequestHandler<GetAvailableDeliveryLotsQuery, OperationResult<IReadOnlyList<AvailableDeliveryLotDto>>>
{
    private readonly IDispatchReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly DeliveryOrderLotInventoryService _inventoryService;

    public GetAvailableDeliveryLotsQueryHandler(
        IDispatchReadDbContext dbContext,
        ICurrentUser currentUser,
        DeliveryOrderLotInventoryService inventoryService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
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
            .Select(x => new { x.ProductId, ProductCode = x.ProductExternalIdSnapshot })
            .FirstOrDefaultAsync(cancellationToken);

        if (line is null)
        {
            return OperationResult<IReadOnlyList<AvailableDeliveryLotDto>>.Fail("Không tìm thấy product/dòng PO trong công ty hiện tại.");
        }

        var canViewCost = DeliveryOrderCostVisibilityRules.CanViewCost(_currentUser);
        var inventory = await _inventoryService.LoadAsync(
            companyId,
            [(line.ProductId, line.ProductCode)],
            includeCost: canViewCost,
            cancellationToken: cancellationToken);
        var result = inventory.Values
            .Where(x => x.AvailableQuantity > 0m && x.ProductAvailableQuantity > 0m)
            .OrderBy(x => x.LotNo)
            .Select(x => new AvailableDeliveryLotDto
            {
                LotNo = x.LotNo,
                OnHandQuantity = x.OnHandQuantity,
                ReservedQuantity = x.ReservedQuantity,
                AvailableQuantity = Math.Min(x.AvailableQuantity, x.ProductAvailableQuantity),
                UnitCostSnapshot = canViewCost ? x.UnitCostSnapshot : null
            })
            .ToList();

        return OperationResult<IReadOnlyList<AvailableDeliveryLotDto>>.Ok(result);
    }
}
