using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;

internal sealed class GetPurchaseOrderByIdQueryHandler(
    IPurchaseOrderDbContext dbContext,
    ICurrentUser currentUser,
    PurchaseOrderReceiptReader receiptReader)
    : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDetailDto?>
{
    public async Task<PurchaseOrderDetailDto?> Handle(
        GetPurchaseOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = PurchaseOrderAccess.RequireCompanyId(currentUser);
        var result = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(item =>
                item.PurchaseOrderId == request.PurchaseOrderId &&
                item.CompanyId == companyId &&
                item.IsActive == true)
            .Select(item => new PurchaseOrderDetailDto
            {
                PurchaseOrderId = item.PurchaseOrderId,
                ExternalId = item.ExternalId ?? string.Empty,
                Status = item.Status ?? string.Empty,
                SupplierId = item.SupplierId,
                SupplierName = item.Supplier == null ? string.Empty : item.Supplier.SupplierName ?? string.Empty,
                RequestDeliveryDate = item.RequestDeliveryDate,
                RealDeliveryDate = item.RealDeliveryDate,
                TotalPrice = item.PurchaseOrderSnapshot == null
                    ? item.PurchaseOrderDetails.Where(detail => detail.IsActive).Sum(detail => detail.TotalPriceAgreed) ?? 0
                    : item.PurchaseOrderSnapshot.TotalPrice ?? 0,
                CreateDate = item.CreateDate,
                MerchandiseOrderCodes = string.Join(", ", item.PurchaseOrderLinks
                    .Where(link => link.IsActive && link.MerchandiseOrder != null)
                    .Select(link => link.MerchandiseOrder!.ExternalId)),
                OrderType = item.OrderType,
                Comment = item.Comment,
                PlpuComment = item.PLPUComment,
                DeliveryAddress = item.PurchaseOrderSnapshot == null ? null : item.PurchaseOrderSnapshot.DeliveryAddress,
                PaymentTypes = item.PurchaseOrderSnapshot == null ? null : item.PurchaseOrderSnapshot.PaymentTypes,
                Vat = item.PurchaseOrderSnapshot == null ? null : item.PurchaseOrderSnapshot.Vat,
                Items = item.PurchaseOrderDetails
                    .Where(detail => detail.IsActive)
                    .OrderBy(detail => detail.LineNo)
                    .Select(detail => new PurchaseOrderLineDto
                    {
                        PurchaseOrderDetailId = detail.PurchaseOrderDetailId,
                        LineNo = detail.LineNo,
                        MaterialId = detail.MaterialId,
                        MaterialCode = detail.MaterialExternalIDSnapshot ?? string.Empty,
                        MaterialName = detail.MaterialNameSnapshot ?? string.Empty,
                        Quantity = detail.RequestQuantity ?? 0,
                        Package = detail.Package,
                        UnitPriceAgreed = detail.UnitPriceAgreed ?? 0,
                        TotalPriceAgreed = detail.TotalPriceAgreed ?? 0,
                        BaseCostSnapshot = detail.BaseCostSnapshot,
                        BaseDateSnapshot = detail.BaseDateSnapshot,
                        DeliveryDate = detail.DeliveryDate,
                        Note = detail.Note
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null || result.ExternalId.Length == 0)
            return result;

        var receipts = await receiptReader.ReadAsync(companyId, [result.ExternalId], cancellationToken);
        foreach (var line in result.Items)
            line.RealQuantity = receipts
                .Where(receipt => receipt.ProductCode == line.MaterialCode)
                .Sum(receipt => receipt.Quantity);

        result.RealDeliveryDate = receipts.Count == 0
            ? result.RealDeliveryDate
            : receipts.Max(receipt => receipt.LastReceiptDate);
        result.RealTotalPrice = result.Items.Sum(line => (line.RealQuantity ?? 0) * line.UnitPriceAgreed);
        return result;
    }
}
