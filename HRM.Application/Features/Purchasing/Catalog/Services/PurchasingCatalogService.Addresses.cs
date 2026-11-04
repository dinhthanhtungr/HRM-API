using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed partial class PurchasingCatalogService
{
    public async Task<OperationResult<CatalogSavedDto>> SaveAddressAsync(Guid supplierId, Guid? id, CatalogAddressWriteRequest request, CancellationToken ct)
    {
        var (companyId, employeeId) = await RequireWriteContextAsync(ct);
        var error = CatalogPatch.Validate(request, "city", "district", "province", "country", "postalCode");
        if (error is not null) return Fail(error);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.LockPurchasingCatalogAsync(companyId, ct);
        var parent = await db.Suppliers.Include(x => x.SupplierAddresses)
            .FirstOrDefaultAsync(x => x.SupplierId == supplierId && x.CompanyId == companyId && x.IsActive == true, ct);
        if (parent is null) return Fail("Không tìm thấy NCC active.");
        if (CatalogPatch.Conflict(request.ExpectedUpdatedDate, parent.UpdatedDate)) return Fail(ConflictMessage);
        var entity = id.HasValue ? parent.SupplierAddresses.FirstOrDefault(x => x.AddressId == id) : null;
        if (id.HasValue && (entity is null || entity.IsActive != true)) return Fail("Không tìm thấy bản ghi active thuộc NCC này.");
        if (entity is null)
        {
            entity = new SupplierAddress { AddressId = Guid.CreateVersion7(), SupplierId = supplierId, IsActive = true, IsPrimary = false };
        }
        CatalogPatch.Text(request.AddressLine, request, nameof(request.AddressLine), () => entity.AddressLine, value => entity.AddressLine = value);
        CatalogPatch.Text(request.City, request, nameof(request.City), () => entity.City, value => entity.City = value);
        CatalogPatch.Text(request.District, request, nameof(request.District), () => entity.District, value => entity.District = value);
        CatalogPatch.Text(request.Province, request, nameof(request.Province), () => entity.Province, value => entity.Province = value);
        CatalogPatch.Text(request.Country, request, nameof(request.Country), () => entity.Country, value => entity.Country = value);
        CatalogPatch.Text(request.PostalCode, request, nameof(request.PostalCode), () => entity.PostalCode, value => entity.PostalCode = value);
        if (string.IsNullOrWhiteSpace(entity.AddressLine)) return Fail("Địa chỉ không được trống.");
        if (request.IsPrimary.HasValue) entity.IsPrimary = request.IsPrimary.Value;
        if (request.IsPrimary == true)
            foreach (var other in parent.SupplierAddresses.Where(x => x.AddressId != entity.AddressId)) other.IsPrimary = false;
        else if (parent.SupplierAddresses.Count(x => x.AddressId != entity.AddressId && x.IsActive == true && x.IsPrimary == true) + (entity.IsPrimary == true ? 1 : 0) > 1)
            return Fail("Dữ liệu có nhiều bản ghi mặc định. Hãy chọn rõ một bản ghi mặc định.");
        if (!id.HasValue)
        {
            parent.SupplierAddresses.Add(entity);
            await db.SupplierAddresses.AddAsync(entity, ct);
        }
        var now = NextUpdate(parent.UpdatedDate);
        parent.UpdatedDate = now; parent.UpdatedBy = employeeId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Saved(entity.AddressId, null, now);
    }
}
