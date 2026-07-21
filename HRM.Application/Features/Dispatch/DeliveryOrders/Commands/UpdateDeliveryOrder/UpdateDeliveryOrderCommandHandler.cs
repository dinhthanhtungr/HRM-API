using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.DeliverySchema;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.UpdateDeliveryOrder
{
    internal sealed class UpdateDeliveryOrderCommandHandler
        : IRequestHandler<UpdateDeliveryOrderCommand, OperationResult<Guid>>
    {
        private readonly IDispatchWriteDbContext _dbContext;

        public UpdateDeliveryOrderCommandHandler(IDispatchWriteDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<OperationResult<Guid>> Handle(
            UpdateDeliveryOrderCommand request,
            CancellationToken cancellationToken)
        {
            if (request.Id == Guid.Empty)
                return OperationResult<Guid>.Fail("DeliveryOrderId không hợp lệ.");

            if (request.UpdatedBy == Guid.Empty)
                return OperationResult<Guid>.Fail("UpdatedBy không hợp lệ.");

            if (request.Lines.Count == 0)
                return OperationResult<Guid>.Fail("Phiếu giao hàng phải có ít nhất một dòng sản phẩm.");

            var deliveryOrder = await _dbContext.DeliveryOrders
                .Include(x => x.Details)
                .Include(x => x.DeliveryOrderPOs)
                .Include(x => x.Deliverers)
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.IsActive, cancellationToken);

            if (deliveryOrder is null)
                return OperationResult<Guid>.Fail("Không tìm thấy phiếu giao hàng.");

            if (string.Equals(deliveryOrder.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return OperationResult<Guid>.Fail("Không thể cập nhật phiếu đã hủy.");

            var requestedLines = request.Lines
                .GroupBy(x => x.MerchandiseOrderDetailId)
                .Select(g => new
                {
                    MerchandiseOrderDetailId = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    NumOfBags = g.Sum(x => x.NumOfBags),
                    LotNoList = string.Join(",", g.Select(x => x.LotNoList).Where(x => !string.IsNullOrWhiteSpace(x)))
                })
                .ToList();

            if (requestedLines.Any(x => x.MerchandiseOrderDetailId == Guid.Empty || x.Quantity <= 0))
                return OperationResult<Guid>.Fail("Dòng giao hàng không hợp lệ.");

            var detailIds = requestedLines.Select(x => x.MerchandiseOrderDetailId).ToArray();

            var orderDetails = await _dbContext.MerchandiseOrderDetails
                .AsNoTracking()
                .Where(x => detailIds.Contains(x.MerchandiseOrderDetailId) && x.IsActive)
                .Select(x => new
                {
                    x.MerchandiseOrderDetailId,
                    x.MerchandiseOrderId,
                    x.ProductId,
                    x.ProductExternalIdSnapshot,
                    x.ProductNameSnapshot,
                    x.ExpectedQuantity,
                    MerchandiseOrderCompanyId = x.MerchandiseOrder.CompanyId,
                    MerchandiseOrderCustomerId = x.MerchandiseOrder.CustomerId,
                    MerchandiseOrderIsActive = x.MerchandiseOrder.IsActive,
                    x.MerchandiseOrder.PONo
                })
                .ToListAsync(cancellationToken);

            if (orderDetails.Count != detailIds.Length)
                return OperationResult<Guid>.Fail("Có dòng PO không tồn tại hoặc đã bị khóa.");

            if (orderDetails.Any(x => !x.MerchandiseOrderIsActive))
                return OperationResult<Guid>.Fail("Có PO đã bị khóa.");

            if (orderDetails.Any(x => x.MerchandiseOrderCompanyId != deliveryOrder.CompanyId))
                return OperationResult<Guid>.Fail("Có dòng PO không cùng công ty với phiếu giao.");

            if (orderDetails.Any(x => x.MerchandiseOrderCustomerId != deliveryOrder.CustomerId))
                return OperationResult<Guid>.Fail("Có dòng PO không cùng khách hàng với phiếu giao.");

            var deliveredQuantities = await _dbContext.DeliveryOrderDetails
                .AsNoTracking()
                .Where(x =>
                    x.DeliveryOrderId != request.Id &&
                    x.IsActive &&
                    !x.IsAttach &&
                    x.MerchandiseOrderDetailId.HasValue &&
                    detailIds.Contains(x.MerchandiseOrderDetailId.Value))
                .GroupBy(x => x.MerchandiseOrderDetailId!.Value)
                .Select(g => new
                {
                    MerchandiseOrderDetailId = g.Key,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .ToListAsync(cancellationToken);

            foreach (var line in requestedLines)
            {
                var detail = orderDetails.Single(x => x.MerchandiseOrderDetailId == line.MerchandiseOrderDetailId);
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
                    return OperationResult<Guid>.Fail("Có người giao hàng không tồn tại hoặc đã khóa.");
            }

            deliveryOrder.Status = string.IsNullOrWhiteSpace(request.Status)
                ? deliveryOrder.Status
                : request.Status.Trim();

            deliveryOrder.Receiver = TrimToNull(request.Receiver);
            deliveryOrder.DeliveryAddress = TrimToNull(request.DeliveryAddress);
            deliveryOrder.PaymentType = TrimToNull(request.PaymentType);
            deliveryOrder.PaymentDeadline = TrimToNull(request.PaymentDeadline);
            deliveryOrder.TaxNumber = TrimToNull(request.TaxNumber);
            deliveryOrder.PhoneSnapshot = TrimToNull(request.PhoneSnapshot);
            deliveryOrder.Note = TrimToNull(request.Note);
            deliveryOrder.DeliveryPrice = request.DeliveryPrice;
            deliveryOrder.RequiresUnloading = request.RequiresUnloading;
            deliveryOrder.UpdatedBy = request.UpdatedBy;
            deliveryOrder.UpdatedDate = DateTime.Now;

            foreach (var oldDetail in deliveryOrder.Details.Where(x => x.IsActive))
            {
                oldDetail.IsActive = false;
            }

            foreach (var oldPo in deliveryOrder.DeliveryOrderPOs.Where(x => x.IsActive))
            {
                oldPo.IsActive = false;
            }

            _dbContext.Deliverers.RemoveRange(deliveryOrder.Deliverers);

            foreach (var line in requestedLines)
            {
                var detail = orderDetails.Single(x => x.MerchandiseOrderDetailId == line.MerchandiseOrderDetailId);

                deliveryOrder.Details.Add(new DeliveryOrderDetail
                {
                    Id = Guid.CreateVersion7(),
                    DeliveryOrderId = deliveryOrder.Id,
                    MerchandiseOrderDetailId = detail.MerchandiseOrderDetailId,
                    ProductId = detail.ProductId,
                    ProductExternalIdSnapShot = detail.ProductExternalIdSnapshot,
                    ProductNameSnapShot = detail.ProductNameSnapshot,
                    PONo = detail.PONo,
                    LotNoList = TrimToNull(line.LotNoList),
                    Quantity = line.Quantity,
                    NumOfBags = line.NumOfBags,
                    IsActive = true,
                    IsAttach = false
                });
            }

            foreach (var merchandiseOrderId in orderDetails.Select(x => x.MerchandiseOrderId).Distinct())
            {
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

            await _dbContext.SaveChangesAsync(cancellationToken);

            return OperationResult<Guid>.Ok(deliveryOrder.Id, "Cập nhật phiếu giao hàng thành công.");
        }

        private static string? TrimToNull(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
