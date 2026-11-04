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
        var result = await BuildQuery(request.PurchaseOrderId, companyId, request.IncludeInactive).FirstOrDefaultAsync(cancellationToken);

        if (result is null || result.ExternalId.Length == 0)
            return result;

        var iqcDates = await receiptReader.ReadIqcDatesAsync(companyId, [result.ExternalId.Trim()], cancellationToken);
        result.RealDeliveryDate = iqcDates.TryGetValue(result.ExternalId.Trim(), out var iqcDate) ? iqcDate : null;
        result.RealTotalPrice = result.Items.Sum(line => (line.RealQuantity ?? 0) * line.UnitPriceAgreed);
        return result;
    }

    internal IQueryable<PurchaseOrderDetailDto> BuildQuery(Guid purchaseOrderId, Guid companyId, bool includeInactive = false)
    {
        return dbContext.PurchaseOrders.AsNoTracking()
            .Where(item =>
                item.PurchaseOrderId == purchaseOrderId &&
                item.CompanyId == companyId &&
                (includeInactive || item.IsActive == true))
            .Select(item => new PurchaseOrderDetailDto
            {
                PurchaseOrderId = item.PurchaseOrderId,
                ExternalId = item.ExternalId ?? string.Empty,
                Status = item.Status ?? string.Empty,
                SupplierId = item.SupplierId,
                SupplierName = item.Supplier == null ? string.Empty : item.Supplier.SupplierName ?? string.Empty,
                RequestDeliveryDate = item.RequestDeliveryDate,
                RealDeliveryDate = null,
                Subtotal = item.PurchaseOrderDetails.Where(detail => includeInactive || detail.IsActive).Sum(detail => detail.TotalPriceAgreed) ?? 0,
                StoredTotalPrice = item.PurchaseOrderSnapshot == null ? null : item.PurchaseOrderSnapshot.TotalPrice,
                CreateDate = item.CreateDate,
                MerchandiseOrderCodes = string.Join(", ", item.PurchaseOrderLinks
                    .Where(link => (includeInactive || link.IsActive) && link.MerchandiseOrder != null &&
                        link.MerchandiseOrder.CompanyId == companyId)
                    .Select(link => link.MerchandiseOrder!.ExternalId)),
                CompanyId = item.CompanyId,
                MerchandiseOrders = item.PurchaseOrderLinks
                    .Where(link => (includeInactive || link.IsActive) && link.MerchandiseOrder != null &&
                        link.MerchandiseOrder.CompanyId == companyId)
                    .OrderBy(link => link.MerchandiseOrder!.ExternalId)
                    .Select(link => new PurchaseOrderMerchandiseOrderDto
                    {
                        MerchandiseOrderId = link.MerchandiseOrder!.MerchandiseOrderId,
                        ExternalId = link.MerchandiseOrder.ExternalId
                    }).ToList(),
                CreatedBy = item.CreatedBy,
                UpdatedBy = item.UpdatedBy,
                UpdatedDate = item.UpdatedDate,
                IsActive = item.IsActive == true,
                OrderType = item.OrderType,
                SupplierExternalId = item.Supplier == null ? null : item.Supplier.ExternalId,
                PurchaseOrderSnapshot = item.PurchaseOrderSnapshot == null ? null : new PurchaseOrderSnapshotDto
                {
                    EmployeeExternalIdSnapshot = item.PurchaseOrderSnapshot.EmployeeExternalIdSnapshot,
                    EmployeeFullNameSnapshot = item.PurchaseOrderSnapshot.EmployeeFullNameSnapshot,
                    PhoneNumberSnapshot = item.PurchaseOrderSnapshot.PhoneNumberSnapshot,
                    EmailSnapshot = item.PurchaseOrderSnapshot.EmailSnapshot,
                    SupplierExternalIdSnapshot = item.PurchaseOrderSnapshot.SupplierExternalIdSnapshot,
                    SupplierNameSnapshot = item.PurchaseOrderSnapshot.SupplierNameSnapshot,
                    SupplierContactSnapshot = item.PurchaseOrderSnapshot.SupplierContactSnapshot,
                    SupplierPhoneNumberSnapshot = item.PurchaseOrderSnapshot.SupplierPhoneNumberSnapshot,
                    SupplierAddressSnapshot = item.PurchaseOrderSnapshot.SupplierAddressSnapshot,
                    TotalPrice = item.PurchaseOrderSnapshot.TotalPrice,
                    DeliveryAddress = item.PurchaseOrderSnapshot.DeliveryAddress,
                    PaymentTypes = item.PurchaseOrderSnapshot.PaymentTypes,
                    Vat = item.PurchaseOrderSnapshot.Vat,
                },
                SupplierContact = item.PurchaseOrderSnapshot == null ? (item.Supplier == null ? null : item.Supplier.SupplierContacts.Where(c => c.IsPrimary == true && c.IsActive == true).Select(c => c.LastName).FirstOrDefault()) : item.PurchaseOrderSnapshot.SupplierContactSnapshot,
                SupplierAddress = item.PurchaseOrderSnapshot == null ? (item.Supplier == null ? null : item.Supplier.SupplierAddresses.Where(a => a.IsPrimary == true && a.IsActive == true).Select(a => a.AddressLine).FirstOrDefault()) : item.PurchaseOrderSnapshot.SupplierAddressSnapshot,
                SupplierPhone = item.PurchaseOrderSnapshot == null ? (item.Supplier == null ? null : item.Supplier.Phone) : item.PurchaseOrderSnapshot.SupplierPhoneNumberSnapshot,
                EmployeeFullName = item.PurchaseOrderSnapshot == null ? (item.CreatedByNavigation == null || item.CreatedByNavigation.CompanyId != companyId ? null : item.CreatedByNavigation.FullName) : item.PurchaseOrderSnapshot.EmployeeFullNameSnapshot,
                EmployeePhone = item.PurchaseOrderSnapshot == null ? (item.CreatedByNavigation == null || item.CreatedByNavigation.CompanyId != companyId ? null : item.CreatedByNavigation.PhoneNumber) : item.PurchaseOrderSnapshot.PhoneNumberSnapshot,
                Comment = item.Comment,
                PlpuComment = item.PLPUComment,
                DeliveryAddress = item.PurchaseOrderSnapshot == null ? null : item.PurchaseOrderSnapshot.DeliveryAddress,
                PaymentTypes = item.PurchaseOrderSnapshot == null ? null : item.PurchaseOrderSnapshot.PaymentTypes,
                Vat = item.PurchaseOrderSnapshot == null ? null : item.PurchaseOrderSnapshot.Vat,
                Items = item.PurchaseOrderDetails
                    .Where(detail => includeInactive || detail.IsActive)
                    .OrderBy(detail => detail.LineNo)
                    .Select(detail => new PurchaseOrderLineDto
                    {
                        PurchaseOrderDetailId = detail.PurchaseOrderDetailId,
                        LineNo = detail.LineNo,
                        MaterialId = detail.MaterialId,
                        MaterialCode = detail.MaterialExternalIDSnapshot ?? string.Empty,
                        MaterialName = detail.MaterialNameSnapshot ?? string.Empty,
                        Quantity = detail.RequestQuantity ?? 0,
                        RealQuantity = detail.RealQuantity,
                        Package = detail.Package,
                        UnitPriceAgreed = detail.UnitPriceAgreed ?? 0,
                        TotalPriceAgreed = detail.TotalPriceAgreed ?? 0,
                        BaseCostSnapshot = detail.BaseCostSnapshot,
                        BaseDateSnapshot = detail.BaseDateSnapshot,
                        DeliveryDate = detail.DeliveryDate,
                        Note = detail.Note
                    })
                    .ToList()
            });
    }
}
