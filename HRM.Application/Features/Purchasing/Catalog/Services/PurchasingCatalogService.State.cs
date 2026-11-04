using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed partial class PurchasingCatalogService
{
    public Task<OperationResult<CatalogSavedDto>> SetSupplierActiveAsync(Guid id, bool active, DateTime? expected, CancellationToken ct)
        => SetActiveAsync(id, active, expected, false, ct);
    public Task<OperationResult<CatalogSavedDto>> SetMaterialActiveAsync(Guid id, bool active, DateTime? expected, CancellationToken ct)
        => SetActiveAsync(id, active, expected, true, ct);

    private async Task<OperationResult<CatalogSavedDto>> SetActiveAsync(Guid id, bool active, DateTime? expected, bool material, CancellationToken ct)
    {
        var (companyId, employeeId) = await RequireWriteContextAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.LockPurchasingCatalogAsync(companyId, ct);
        string? code;
        var now = NextUpdate();
        if (material)
        {
            var entity = await db.Materials.FirstOrDefaultAsync(x => x.MaterialId == id && x.CompanyId == companyId, ct);
            if (entity is null) return Fail("Không tìm thấy vật tư.");
            if (CatalogPatch.Conflict(expected, entity.UpdatedDate)) return Fail(ConflictMessage);
            now = NextUpdate(entity.UpdatedDate);
            if (active && !await db.Categories.AnyAsync(x => x.CategoryId == entity.CategoryId && x.CompanyId == companyId && x.IsActive == true, ct))
                return Fail("Không thể khôi phục vật tư có nhóm đã ngừng hoặc khác công ty.");
            if (active)
            {
                var normalizedCode = entity.ExternalId?.ToLowerInvariant();
                var normalizedBarcode = string.IsNullOrWhiteSpace(entity.Barcode) ? null : entity.Barcode.ToLowerInvariant();
                if (await db.Materials.AnyAsync(x => x.CompanyId == companyId && x.MaterialId != id && x.IsActive == true &&
                    (normalizedCode != null && x.ExternalId != null && x.ExternalId.ToLower() == normalizedCode ||
                     normalizedBarcode != null && x.Barcode != null && x.Barcode.ToLower() == normalizedBarcode), ct))
                    return Fail("Vật tư khác đang dùng cùng mã hoặc barcode, không thể khôi phục.");
            }
            code = entity.ExternalId;
            entity.IsActive = active; entity.UpdatedBy = employeeId; entity.UpdatedDate = now;
        }
        else
        {
            var entity = await db.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id && x.CompanyId == companyId, ct);
            if (entity is null) return Fail("Không tìm thấy NCC.");
            if (CatalogPatch.Conflict(expected, entity.UpdatedDate)) return Fail(ConflictMessage);
            now = NextUpdate(entity.UpdatedDate);
            if (active && entity.ExternalId is not null)
            {
                var normalizedCode = entity.ExternalId.ToLowerInvariant();
                if (await db.Suppliers.AnyAsync(x => x.CompanyId == companyId && x.SupplierId != id && x.IsActive == true &&
                    x.ExternalId != null && x.ExternalId.ToLower() == normalizedCode, ct))
                    return Fail("NCC khác đang dùng cùng mã, không thể khôi phục.");
            }
            code = entity.ExternalId;
            entity.IsActive = active; entity.UpdatedBy = employeeId; entity.UpdatedDate = now;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Saved(id, code, now);
    }
}
