using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Abstractions.Persistence.Purchasing;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrderPdf;

internal sealed class ExportPurchaseOrderPdfQueryHandler(
    IPurchaseOrderDbContext dbContext,
    ICurrentUser currentUser,
    IPurchaseOrderPdfRenderer renderer,
    PurchaseOrderPrintService workflow,
    ICurrentUserPermissionService permissions)
    : IRequestHandler<ExportPurchaseOrderPdfQuery, OperationResult<PurchaseOrderFileDto>>
{
    public async Task<OperationResult<PurchaseOrderFileDto>> Handle(
        ExportPurchaseOrderPdfQuery request,
        CancellationToken cancellationToken)
    {
        if (!PurchaseOrderAccess.CanManage(permissions))
            return OperationResult<PurchaseOrderFileDto>.Fail("Bạn không có quyền xuất PO.");

        var companyId = PurchaseOrderAccess.RequireCompanyId(currentUser);
        var purchaseOrder = await BuildQuery(request.PurchaseOrderId, companyId).FirstOrDefaultAsync(cancellationToken);
        if (purchaseOrder is null)
            return OperationResult<PurchaseOrderFileDto>.Fail("Không tìm thấy đơn mua hàng.");

        await workflow.EnsureForPrintAsync(request.PurchaseOrderId, cancellationToken);

        return OperationResult<PurchaseOrderFileDto>.Ok(new PurchaseOrderFileDto
        {
            FileName = $"PO-{Sanitize(purchaseOrder.PurchaseOrderExternalIdSnapshot)}.pdf",
            ContentType = "application/pdf",
            Content = renderer.Render(purchaseOrder)
        });
    }

    // Preserve nullable persisted amounts for the legacy renderer's null-only fallbacks.
    internal IQueryable<PurchaseOrderPdfDocument> BuildQuery(Guid purchaseOrderId, Guid companyId) =>
        dbContext.PurchaseOrders.AsNoTracking()
            .Where(x => x.PurchaseOrderId == purchaseOrderId && x.CompanyId == companyId && x.IsActive == true)
            .Select(x => new PurchaseOrderPdfDocument
            {
                PurchaseOrderSnapshotId = x.PurchaseOrderSnapshotId ?? Guid.Empty,
                PurchaseOrderExternalIdSnapshot = x.ExternalId ?? string.Empty,
                EmployeeExternalIdSnapshot = x.PurchaseOrderSnapshot != null
                    ? x.PurchaseOrderSnapshot.EmployeeExternalIdSnapshot
                    : x.CreatedByNavigation != null && x.CreatedByNavigation.CompanyId == companyId ? x.CreatedByNavigation.ExternalId : null,
                EmployeeFullNameSnapshot = x.PurchaseOrderSnapshot != null
                    ? x.PurchaseOrderSnapshot.EmployeeFullNameSnapshot
                    : x.CreatedByNavigation != null && x.CreatedByNavigation.CompanyId == companyId ? x.CreatedByNavigation.FullName : null,
                PhoneNumberSnapshot = x.PurchaseOrderSnapshot != null
                    ? x.PurchaseOrderSnapshot.PhoneNumberSnapshot
                    : x.CreatedByNavigation != null && x.CreatedByNavigation.CompanyId == companyId ? x.CreatedByNavigation.PhoneNumber : null,
                EmailSnapshot = x.PurchaseOrderSnapshot != null
                    ? x.PurchaseOrderSnapshot.EmailSnapshot
                    : x.CreatedByNavigation != null && x.CreatedByNavigation.CompanyId == companyId ? x.CreatedByNavigation.Email : null,

                SupplierExternalIdSnapshot = x.PurchaseOrderSnapshot != null
                    ? x.PurchaseOrderSnapshot.SupplierExternalIdSnapshot
                    : x.Supplier == null ? null : x.Supplier.ExternalId,
                SupplierNameSnapshot = x.PurchaseOrderSnapshot != null
                    ? x.PurchaseOrderSnapshot.SupplierNameSnapshot
                    : x.Supplier == null ? null : x.Supplier.SupplierName,
                SupplierContactSnapshot = x.PurchaseOrderSnapshot != null
                    ? x.PurchaseOrderSnapshot.SupplierContactSnapshot
                    : x.Supplier == null ? null : x.Supplier.SupplierContacts.Where(c => c.IsPrimary == true && c.IsActive == true).Select(c => c.LastName).FirstOrDefault(),
                SupplierPhoneNumberSnapshot = x.PurchaseOrderSnapshot != null
                    ? x.PurchaseOrderSnapshot.SupplierPhoneNumberSnapshot
                    : x.Supplier == null ? null : x.Supplier.Phone,
                SupplierAddressSnapshot = x.PurchaseOrderSnapshot != null
                    ? x.PurchaseOrderSnapshot.SupplierAddressSnapshot
                    : x.Supplier == null ? null : x.Supplier.SupplierAddresses.Where(c => c.IsPrimary == true && c.IsActive == true).Select(c => c.AddressLine).FirstOrDefault(),

                Note = x.Comment,

                TotalPrice = x.PurchaseOrderSnapshot != null
                    ? x.PurchaseOrderSnapshot.TotalPrice
                    : null,

                DeliveryAddress = x.PurchaseOrderSnapshot != null ? x.PurchaseOrderSnapshot.DeliveryAddress : null,
                PaymentTypes = x.PurchaseOrderSnapshot != null ? x.PurchaseOrderSnapshot.PaymentTypes : null,
                Vat = x.PurchaseOrderSnapshot != null ? x.PurchaseOrderSnapshot.Vat : 0,

                CreatedDate = x.CreateDate,
                RequestDeliveryDate = x.RequestDeliveryDate,

                Details = x.PurchaseOrderDetails
                    .Where(d => d.PurchaseOrderId == x.PurchaseOrderId && d.IsActive)
                    .OrderBy(d => d.LineNo)
                    .Select(d => new PurchaseOrderPdfLine
                    {
                        MaterialId = d.MaterialId,
                        MaterialExternalIDSnapshot = d.MaterialExternalIDSnapshot,
                        MaterialNameSnapshot = d.MaterialNameSnapshot,
                        Package = d.Package,
                        BaseCostSnapshot = d.BaseCostSnapshot,
                        BaseDateSnapshot = d.BaseDateSnapshot,
                        RequestQuantity = d.RequestQuantity,
                        UnitPriceAgreed = d.UnitPriceAgreed,
                        TotalUnitPriceAgreed = d.TotalPriceAgreed
                    })
                    .ToList()
            });
    private static string Sanitize(string value) =>
        string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
}
