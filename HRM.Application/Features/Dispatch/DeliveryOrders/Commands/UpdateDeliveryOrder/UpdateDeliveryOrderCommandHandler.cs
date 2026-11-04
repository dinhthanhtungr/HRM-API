using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Dispatch.DeliveryOrders.Services;
using HRM.Domain.Entities.DeliverySchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.UpdateDeliveryOrder;

/// <summary>
/// Cập nhật phiếu giao hàng trong company hiện tại và đồng bộ toàn bộ lot trong cùng transaction.
/// </summary>
internal sealed class UpdateDeliveryOrderCommandHandler
    : IRequestHandler<UpdateDeliveryOrderCommand, OperationResult<Guid>>
{
    private readonly IDispatchWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly DeliveryOrderLotInventoryService _inventoryService;
    private readonly DeliveryWarehouseRequestService _warehouseRequests;
    private readonly DeliveryOrderFulfillmentService _fulfillment;

    public UpdateDeliveryOrderCommandHandler(
        IDispatchWriteDbContext dbContext,
        ICurrentUser currentUser,
        DeliveryOrderLotInventoryService inventoryService,
        DeliveryWarehouseRequestService warehouseRequests,
        DeliveryOrderFulfillmentService fulfillment)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _inventoryService = inventoryService;
        _warehouseRequests = warehouseRequests;
        _fulfillment = fulfillment;
    }

    public async Task<OperationResult<Guid>> Handle(
        UpdateDeliveryOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (!DeliveryOrderAccessRules.CanManage(_currentUser))
        {
            return OperationResult<Guid>.Fail("Bạn không có quyền cập nhật phiếu giao hàng.");
        }

        if (!TryGetCurrentScope(out var companyId, out var employeeId, out var scopeError))
        {
            return OperationResult<Guid>.Fail(scopeError!);
        }

        if (request.Id == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("DeliveryOrderId không hợp lệ.");
        }

        if (!DeliveryOrderLotNormalizer.TryNormalize(
                request.Lines,
                out var requestedLines,
                out var lineError))
        {
            return OperationResult<Guid>.Fail(lineError!);
        }

        await using var transaction = await _dbContext.BeginDeliveryOrderTransactionAsync(cancellationToken);
        if (await _dbContext.DeliveryOrders.Where(x => x.Id == request.Id && x.CompanyId == companyId &&
                x.IsActive && x.Status == "Pending" && !x.HasPrinted)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.HasPrinted, x => x.HasPrinted), cancellationToken) == 0)
            return OperationResult<Guid>.Fail("Chỉ sửa nội dung phiếu Pending chưa in; phiếu đã in dùng PATCH số bao/người giao.");

        var deliveryOrder = await _dbContext.DeliveryOrders
            .Include(x => x.Details)
                .ThenInclude(x => x.LotConsumptions)
            .Include(x => x.DeliveryOrderPOs)
            .Include(x => x.Deliverers)
            .FirstOrDefaultAsync(x =>
                x.Id == request.Id &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);

        if (deliveryOrder is null)
        {
            return OperationResult<Guid>.Fail("Không tìm thấy phiếu giao hàng.");
        }

        if (!DeliveryOrderLifecycleRules.CanEditContent(deliveryOrder.Status))
        {
            return OperationResult<Guid>.Fail(
                "Chỉ có thể sửa lot, quantity và nội dung khi phiếu giao ở trạng thái Pending.");
        }

        var detailIds = requestedLines.Select(x => x.MerchandiseOrderDetailId).ToArray();
        var orderDetails = await _dbContext.MerchandiseOrderDetails
            .AsNoTracking()
            .Where(x =>
                detailIds.Contains(x.MerchandiseOrderDetailId) &&
                x.IsActive &&
                x.MerchandiseOrder.CompanyId == companyId)
            .Select(x => new
            {
                x.MerchandiseOrderDetailId,
                x.MerchandiseOrderId,
                x.ProductId,
                x.ProductExternalIdSnapshot,
                x.ProductNameSnapshot,
                MerchandiseOrderCustomerId = x.MerchandiseOrder.CustomerId,
                MerchandiseOrderIsActive = x.MerchandiseOrder.IsActive,
                x.MerchandiseOrder.PONo
            })
            .ToListAsync(cancellationToken);

        if (orderDetails.Count != detailIds.Length)
        {
            return OperationResult<Guid>.Fail("Có dòng PO không tồn tại hoặc đã bị khóa.");
        }

        if (orderDetails.Any(x => !x.MerchandiseOrderIsActive))
        {
            return OperationResult<Guid>.Fail("Có PO đã bị khóa.");
        }

        if (orderDetails.Any(x => x.MerchandiseOrderCustomerId != deliveryOrder.CustomerId))
        {
            return OperationResult<Guid>.Fail("Có dòng PO không cùng khách hàng với phiếu giao.");
        }

        // Cho phép giao vượt lượng đặt; giới hạn lượng giao vẫn được kiểm tra theo tồn lot/loại hàng.

        var delivererIds = request.DelivererInforIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (delivererIds.Length > 0)
        {
            var validDelivererCount = await _dbContext.DelivererInfors
                .AsNoTracking()
                .CountAsync(x => delivererIds.Contains(x.Id) && x.IsActive, cancellationToken);

            if (validDelivererCount != delivererIds.Length)
            {
                return OperationResult<Guid>.Fail("Có người giao hàng không tồn tại hoặc đã khóa.");
            }
        }

        if (DeliveryOrderUpdateRules.HasSameContent(
                deliveryOrder,
                request,
                requestedLines,
                delivererIds) && await _warehouseRequests.HasSameAllocationsAsync(deliveryOrder, requestedLines, cancellationToken))
        {
            await _fulfillment.SyncAsync(deliveryOrder.Id, companyId, employeeId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<Guid>.Ok(
                deliveryOrder.Id,
                "Phiếu giao hàng đã có đúng nội dung yêu cầu.");
        }

        var requestedInventoryLots = requestedLines
            .SelectMany(line =>
            {
                var detail = orderDetails.Single(x =>
                    x.MerchandiseOrderDetailId == line.MerchandiseOrderDetailId);
                return line.Lots.Select(lot => new DeliveryOrderRequestedLot(
                    detail.ProductId,
                    detail.ProductExternalIdSnapshot,
                    lot.LotNo,
                    lot.Quantity, lot.StockType));
            })
            .ToArray();
        var inventory = await _inventoryService.LoadAsync(
            companyId,
            orderDetails.Select(x => (x.ProductId, x.ProductExternalIdSnapshot)).ToArray(),
            includeCost: true,
            cancellationToken: cancellationToken);
        var inventoryError = DeliveryOrderLotInventoryRules.Validate(requestedInventoryLots, inventory);
        if (inventoryError is not null)
        {
            return OperationResult<Guid>.Fail(inventoryError);
        }

        var now = DateTime.Now;
        deliveryOrder.Receiver = TrimToNull(request.Receiver);
        deliveryOrder.DeliveryAddress = TrimToNull(request.DeliveryAddress);
        deliveryOrder.PaymentType = TrimToNull(request.PaymentType);
        deliveryOrder.PaymentDeadline = TrimToNull(request.PaymentDeadline);
        deliveryOrder.TaxNumber = TrimToNull(request.TaxNumber);
        deliveryOrder.PhoneSnapshot = TrimToNull(request.PhoneSnapshot);
        deliveryOrder.Note = TrimToNull(request.Note);
        deliveryOrder.DeliveryPrice = request.DeliveryPrice;
        deliveryOrder.RequiresUnloading = request.RequiresUnloading;
        deliveryOrder.UpdatedBy = employeeId;
        deliveryOrder.UpdatedDate = now;

        foreach (var oldDetail in deliveryOrder.Details.Where(x => x.IsActive && !x.IsAttach))
        {
            oldDetail.IsActive = false;
            foreach (var oldLot in oldDetail.LotConsumptions.Where(x => x.IsActive))
            {
                oldLot.IsActive = false;
            }
        }

        foreach (var oldPo in deliveryOrder.DeliveryOrderPOs.Where(x => x.IsActive))
        {
            oldPo.IsActive = false;
        }

        _dbContext.Deliverers.RemoveRange(deliveryOrder.Deliverers);

        foreach (var line in requestedLines)
        {
            var sourceDetail = orderDetails.Single(x =>
                x.MerchandiseOrderDetailId == line.MerchandiseOrderDetailId);
            var deliveryDetail = new DeliveryOrderDetail
            {
                Id = Guid.CreateVersion7(),
                DeliveryOrderId = deliveryOrder.Id,
                MerchandiseOrderDetailId = sourceDetail.MerchandiseOrderDetailId,
                ProductId = sourceDetail.ProductId,
                ProductExternalIdSnapShot = sourceDetail.ProductExternalIdSnapshot,
                ProductNameSnapShot = sourceDetail.ProductNameSnapshot,
                PONo = sourceDetail.PONo,
                LotNoList = line.LotNoList,
                Quantity = line.Quantity,
                NumOfBags = line.NumOfBags,
                IsActive = true,
                IsAttach = false
            };

            AddLotConsumptions(
                deliveryDetail,
                line.Lots,
                sourceDetail.ProductExternalIdSnapshot,
                inventory,
                employeeId,
                now);
            deliveryOrder.Details.Add(deliveryDetail);
        }

        foreach (var merchandiseOrderId in orderDetails.Select(x => x.MerchandiseOrderId).Distinct())
        {
            // Khóa kép không cho phép tạo lại liên kết đã tồn tại, kể cả liên kết inactive.
            var existingPo = deliveryOrder.DeliveryOrderPOs.SingleOrDefault(x =>
                x.MerchandiseOrderId == merchandiseOrderId);
            if (existingPo is not null)
            {
                existingPo.IsActive = true;
                continue;
            }

            deliveryOrder.DeliveryOrderPOs.Add(new DeliveryOrderPO
            {
                DeliveryOrderId = deliveryOrder.Id,
                MerchandiseOrderId = merchandiseOrderId,
                IsActive = true
            });
        }

        foreach (var delivererId in delivererIds)
        {
            deliveryOrder.Deliverers.Add(new Deliverer
            {
                Id = Guid.CreateVersion7(),
                DeliveryOrderId = deliveryOrder.Id,
                DelivererInforId = delivererId
            });
        }

        if (!await _warehouseRequests.SyncPendingAsync(deliveryOrder, cancellationToken, requestedLines))
            return OperationResult<Guid>.Fail("Không thể đồng bộ xuất kho: PRQ đã xử lý, có nhiều PRQ liên kết hoặc phiếu thiếu mã liên kết.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _fulfillment.SyncAsync(deliveryOrder.Id, companyId, employeeId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return OperationResult<Guid>.Ok(deliveryOrder.Id, "Cập nhật phiếu giao hàng thành công.");
    }

    private bool TryGetCurrentScope(
        out Guid companyId,
        out Guid employeeId,
        out string? error)
    {
        companyId = _currentUser.CompanyId ?? Guid.Empty;
        employeeId = _currentUser.EmployeeId ?? Guid.Empty;
        error = null;

        if (!_currentUser.IsAuthenticated || companyId == Guid.Empty || employeeId == Guid.Empty)
        {
            error = "Tài khoản hiện tại chưa được liên kết đầy đủ với công ty và nhân viên.";
            return false;
        }

        return true;
    }

    private static void AddLotConsumptions(
        DeliveryOrderDetail detail,
        IReadOnlyList<NormalizedDeliveryLot> lots,
        string productCode,
        IReadOnlyDictionary<string, DeliveryOrderLotInventorySnapshot> inventory,
        Guid employeeId,
        DateTime createdDate)
    {
        foreach (var group in lots.GroupBy(x => DeliveryOrderLotInventoryRules.Normalize(x.LotNo)))
        {
            var quantity = group.Sum(x => x.Quantity);
            var totalCost = group.Sum(lot => DeliveryOrderLotInventoryRules.CalculateTotalCost(lot.Quantity,
                inventory[DeliveryOrderLotInventoryRules.Key(productCode, lot.LotNo, lot.StockType)].UnitCostSnapshot));
            detail.LotConsumptions.Add(new DeliveryOrderDetailLotConsumption
            {
                Id = Guid.CreateVersion7(), DeliveryOrderDetailId = detail.Id,
                LotNo = group.First().LotNo, Quantity = quantity,
                UnitCostSnapshot = quantity == 0m ? 0m : totalCost / quantity,
                TotalCostSnapshot = totalCost, CreatedBy = employeeId,
                CreatedDate = createdDate, IsActive = true
            });
        }
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
