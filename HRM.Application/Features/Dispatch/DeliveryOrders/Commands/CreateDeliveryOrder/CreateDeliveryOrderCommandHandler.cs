using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Enums.Deliveries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.CreateDeliveryOrder;

/// <summary>
/// Tạo phiếu giao hàng trong company hiện tại và lưu chi tiết lot cùng một transaction.
/// </summary>
internal sealed class CreateDeliveryOrderCommandHandler
    : IRequestHandler<CreateDeliveryOrderCommand, OperationResult<Guid>>
{
    private readonly IDispatchWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly DeliveryOrderLotInventoryService _inventoryService;

    public CreateDeliveryOrderCommandHandler(
        IDispatchWriteDbContext dbContext,
        ICurrentUser currentUser,
        DeliveryOrderLotInventoryService inventoryService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _inventoryService = inventoryService;
    }

    public async Task<OperationResult<Guid>> Handle(
        CreateDeliveryOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (!DeliveryOrderAccessRules.CanManage(_currentUser))
        {
            return OperationResult<Guid>.Fail("Bạn không có quyền tạo phiếu giao hàng.");
        }

        if (!TryGetCurrentScope(out var companyId, out var employeeId, out var scopeError))
        {
            return OperationResult<Guid>.Fail(scopeError!);
        }

        if (request.CustomerId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("CustomerId không hợp lệ.");
        }

        if (!DeliveryOrderLotNormalizer.TryNormalize(
                request.Lines,
                out var requestedLines,
                out var lineError))
        {
            return OperationResult<Guid>.Fail(lineError!);
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
                x.ExpectedQuantity,
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

        if (orderDetails.Any(x => x.MerchandiseOrderCustomerId != request.CustomerId))
        {
            return OperationResult<Guid>.Fail("Có dòng PO không cùng khách hàng với phiếu giao.");
        }

        var deliveredQuantities = await _dbContext.DeliveryOrderDetails
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                !x.IsAttach &&
                x.DeliveryOrder.IsActive &&
                x.DeliveryOrder.CompanyId == companyId &&
                x.MerchandiseOrderDetailId.HasValue &&
                detailIds.Contains(x.MerchandiseOrderDetailId.Value))
            .GroupBy(x => x.MerchandiseOrderDetailId!.Value)
            .Select(group => new
            {
                MerchandiseOrderDetailId = group.Key,
                Quantity = group.Sum(x => x.Quantity)
            })
            .ToListAsync(cancellationToken);

        foreach (var line in requestedLines)
        {
            var detail = orderDetails.Single(x =>
                x.MerchandiseOrderDetailId == line.MerchandiseOrderDetailId);
            var delivered = deliveredQuantities
                .FirstOrDefault(x => x.MerchandiseOrderDetailId == line.MerchandiseOrderDetailId)
                ?.Quantity ?? 0m;
            var remaining = detail.ExpectedQuantity - delivered;

            if (line.Quantity > remaining)
            {
                return OperationResult<Guid>.Fail(
                    $"Số lượng giao vượt quá còn lại. Product: {detail.ProductExternalIdSnapshot}, còn lại: {remaining}.");
            }
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
                    lot.Quantity));
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

        var now = DateTime.Now;
        var deliveryOrderId = Guid.CreateVersion7();
        var deliveryOrder = new DeliveryOrder
        {
            Id = deliveryOrderId,
            ExternalId = TrimToNull(request.ExternalId),
            Status = DeliveryOrderStatus.Pending.ToString(),
            CustomerId = request.CustomerId,
            CustomerExternalIdSnapShot = TrimToNull(request.CustomerExternalIdSnapShot),
            Receiver = TrimToNull(request.Receiver),
            DeliveryAddress = TrimToNull(request.DeliveryAddress),
            PaymentType = TrimToNull(request.PaymentType),
            PaymentDeadline = TrimToNull(request.PaymentDeadline),
            TaxNumber = TrimToNull(request.TaxNumber),
            PhoneSnapshot = TrimToNull(request.PhoneSnapshot),
            Note = TrimToNull(request.Note),
            DeliveryPrice = request.DeliveryPrice,
            RequiresUnloading = request.RequiresUnloading,
            CompanyId = companyId,
            CreatedBy = employeeId,
            CreatedDate = now,
            IsActive = true,
            HasPrinted = false
        };

        foreach (var line in requestedLines)
        {
            var sourceDetail = orderDetails.Single(x =>
                x.MerchandiseOrderDetailId == line.MerchandiseOrderDetailId);
            var deliveryDetail = new DeliveryOrderDetail
            {
                Id = Guid.CreateVersion7(),
                DeliveryOrderId = deliveryOrderId,
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
            deliveryOrder.DeliveryOrderPOs.Add(new DeliveryOrderPO
            {
                DeliveryOrderId = deliveryOrderId,
                MerchandiseOrderId = merchandiseOrderId,
                IsActive = true
            });
        }

        foreach (var delivererId in delivererIds)
        {
            deliveryOrder.Deliverers.Add(new Deliverer
            {
                Id = Guid.CreateVersion7(),
                DeliveryOrderId = deliveryOrderId,
                DelivererInforId = delivererId
            });
        }

        await using var transaction = await _dbContext.BeginDeliveryOrderTransactionAsync(cancellationToken);
        _dbContext.DeliveryOrders.Add(deliveryOrder);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return OperationResult<Guid>.Ok(deliveryOrderId, "Tạo phiếu giao hàng thành công.");
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
        foreach (var lot in lots)
        {
            var unitCost = inventory[DeliveryOrderLotInventoryRules.Key(productCode, lot.LotNo)]
                .UnitCostSnapshot;
            detail.LotConsumptions.Add(new DeliveryOrderDetailLotConsumption
            {
                Id = Guid.CreateVersion7(),
                DeliveryOrderDetailId = detail.Id,
                LotNo = lot.LotNo,
                Quantity = lot.Quantity,
                UnitCostSnapshot = unitCost,
                TotalCostSnapshot = DeliveryOrderLotInventoryRules.CalculateTotalCost(lot.Quantity, unitCost),
                CreatedBy = employeeId,
                CreatedDate = createdDate,
                IsActive = true
            });
        }
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
