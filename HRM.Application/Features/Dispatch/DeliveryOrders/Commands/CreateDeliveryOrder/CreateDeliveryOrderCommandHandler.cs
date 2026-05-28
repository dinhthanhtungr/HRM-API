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

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Commands.CreateDeliveryOrder
{
    internal sealed class CreateDeliveryOrderCommandHandler
        : IRequestHandler<CreateDeliveryOrderCommand, OperationResult<Guid>>
    {
        private readonly IDispatchWriteDbContext _dbContext;

        public CreateDeliveryOrderCommandHandler(IDispatchWriteDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<OperationResult<Guid>> Handle(
            CreateDeliveryOrderCommand request,
            CancellationToken cancellationToken)
        {
            if (request.CustomerId == Guid.Empty)
                return OperationResult<Guid>.Fail("CustomerId không hợp lệ.");

            if (request.CompanyId == Guid.Empty)
                return OperationResult<Guid>.Fail("CompanyId không hợp lệ.");

            if (request.CreatedBy == Guid.Empty)
                return OperationResult<Guid>.Fail("CreatedBy không hợp lệ.");

            if (request.Lines.Count == 0)
                return OperationResult<Guid>.Fail("Phiếu giao hàng phải có ít nhất một dòng sản phẩm.");

            if (request.Lines.Any(x => x.MerchandiseOrderDetailId == Guid.Empty || x.Quantity <= 0))
                return OperationResult<Guid>.Fail("Dòng giao hàng không hợp lệ.");

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

            if (orderDetails.Any(x => x.MerchandiseOrderCompanyId != request.CompanyId))
                return OperationResult<Guid>.Fail("Có dòng PO không cùng công ty với phiếu giao.");

            if (orderDetails.Any(x => x.MerchandiseOrderCustomerId != request.CustomerId))
                return OperationResult<Guid>.Fail("Có dòng PO không cùng khách hàng với phiếu giao.");

            var deliveredQuantities = await _dbContext.DeliveryOrderDetails
                .AsNoTracking()
                .Where(x =>
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

            var deliveryOrderId = Guid.CreateVersion7();

            var deliveryOrder = new DeliveryOrder
            {
                Id = deliveryOrderId,
                ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim(),
                Status = string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status.Trim(),
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
                CompanyId = request.CompanyId,
                CreatedBy = request.CreatedBy,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                HasPrinted = false
            };

            foreach (var line in requestedLines)
            {
                var detail = orderDetails.Single(x => x.MerchandiseOrderDetailId == line.MerchandiseOrderDetailId);

                deliveryOrder.Details.Add(new DeliveryOrderDetail
                {
                    Id = Guid.CreateVersion7(),
                    DeliveryOrderId = deliveryOrderId,
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

            _dbContext.DeliveryOrders.Add(deliveryOrder);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return OperationResult<Guid>.Ok(deliveryOrderId, "Tạo phiếu giao hàng thành công.");
        }

        private static string? TrimToNull(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
