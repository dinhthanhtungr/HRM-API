using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed partial class PurchasingCatalogService
{
    public async Task<OperationResult<CatalogSavedDto>> SaveMaterialAsync(Guid? id, CatalogMaterialWriteRequest request, CancellationToken ct)
    {
        if (request.SupplierPrices is { Count: > 0 })
        {
            var aggregate = await SaveMaterialWithSupplierPricesAsync(id,
                new CatalogMaterialAggregateRequest { Material = request, SupplierPrices = request.SupplierPrices }, ct);
            return aggregate.Success ? OperationResult<CatalogSavedDto>.Ok(aggregate.Data!.Material) : Fail(aggregate.Message ?? "Không lưu được NVL và giá NCC.");
        }
        var (companyId, employeeId) = await RequireWriteContextAsync(ct);
        if (request.SupplierPrices is null) return Fail("SupplierPrices không được null; bỏ field hoặc gửi [] nếu chỉ lưu master.");
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.LockPurchasingCatalogAsync(companyId, ct);
        var result = await StageMaterialAsync(id, request, companyId, employeeId, ct);
        if (!result.Success) return result;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<OperationResult<CatalogSavedDto>> StageMaterialAsync(Guid? id, CatalogMaterialWriteRequest request,
        Guid companyId, Guid employeeId, CancellationToken ct)
    {
        var error = CatalogPatch.Validate(request, "customCode", "unit", "package", "comment", "barcode", "weight", "minQuantity");
        if (error is not null) return Fail(error);
        if ((!id.HasValue || request.Name is not null) && string.IsNullOrWhiteSpace(request.Name)) return Fail("Tên vật tư không được trống.");
        if (request.Weight.HasValue && !double.IsFinite(request.Weight.Value) || request.MinQuantity.HasValue && !double.IsFinite(request.MinQuantity.Value))
            return Fail("Khối lượng và tồn tối thiểu phải là số hữu hạn không âm.");
        if (id.HasValue && request.ExternalId is not null) return Fail("Không được đổi mã vật tư.");
        if (!id.HasValue && request.ExpectedUpdatedDate.HasValue) return Fail("Tạo mới không nhận phiên bản cập nhật.");
        var entity = id.HasValue ? await db.Materials.FirstOrDefaultAsync(x => x.MaterialId == id && x.CompanyId == companyId, ct) : null;
        if (id.HasValue && (entity is null || entity.IsActive != true)) return Fail("Không tìm thấy vật tư active. Hãy khôi phục trước khi sửa.");
        if (entity is not null && CatalogPatch.Conflict(request.ExpectedUpdatedDate, entity.UpdatedDate)) return Fail(ConflictMessage);
        var categoryId = request.CategoryId ?? entity?.CategoryId;
        var category = await db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.CategoryId == categoryId && x.CompanyId == companyId && x.IsActive == true, ct);
        if (category is null) return Fail("Nhóm vật tư phải active và thuộc công ty hiện tại.");
        var barcode = request.Barcode?.Trim();
        if (!string.IsNullOrEmpty(barcode))
        {
            var normalizedBarcode = barcode.ToLowerInvariant();
            if (await db.Materials.AnyAsync(x => x.CompanyId == companyId && x.MaterialId != (id ?? Guid.Empty) && x.Barcode != null && x.Barcode.ToLower() == normalizedBarcode, ct))
                return Fail("Barcode đã được dùng bởi vật tư khác, kể cả vật tư đã ngừng.");
        }
        var now = NextUpdate(entity?.UpdatedDate);
        if (entity is null)
        {
            var prefix = string.IsNullOrWhiteSpace(category.ExternalId) ? "NVL" : $"NVL_{category.ExternalId.Trim().ToUpperInvariant()}";
            if (string.IsNullOrWhiteSpace(request.ExternalId))
                await db.EnsurePurchasingCatalogCounterAsync(prefix, true, ct);
            var code = string.IsNullOrWhiteSpace(request.ExternalId) ? await codes.GenerateGlobalCodeAsync(companyId, prefix, ct) : request.ExternalId.Trim();
            var normalizedCode = code.ToLowerInvariant();
            if (await db.Materials.AnyAsync(x => x.CompanyId == companyId && x.ExternalId != null && x.ExternalId.ToLower() == normalizedCode, ct))
                return Fail("Mã vật tư đã tồn tại, kể cả trong lịch sử ngừng hoạt động.");
            entity = new Material { MaterialId = Guid.CreateVersion7(), CompanyId = companyId, ExternalId = code, CategoryId = category.CategoryId,
                IsActive = true, CreatedBy = employeeId, CreatedDate = now };
            await db.Materials.AddAsync(entity, ct);
        }
        CatalogPatch.Text(request.Name, request, nameof(request.Name), () => entity.Name, value => entity.Name = value);
        CatalogPatch.Text(request.CustomCode, request, nameof(request.CustomCode), () => entity.CustomCode, value => entity.CustomCode = value);
        CatalogPatch.Text(request.Unit, request, nameof(request.Unit), () => entity.Unit, value => entity.Unit = value);
        CatalogPatch.Text(request.Package, request, nameof(request.Package), () => entity.Package, value => entity.Package = value);
        CatalogPatch.Text(request.Comment, request, nameof(request.Comment), () => entity.Comment, value => entity.Comment = value);
        CatalogPatch.Text(request.Barcode, request, nameof(request.Barcode), () => entity.Barcode, value => entity.Barcode = value);
        CatalogPatch.Number(request.Weight, request, nameof(request.Weight), () => entity.Weight, value => entity.Weight = value);
        CatalogPatch.Number(request.MinQuantity, request, nameof(request.MinQuantity), () => entity.MinQuantity, value => entity.MinQuantity = value);
        entity.CategoryId = category.CategoryId;
        entity.UpdatedBy = employeeId;
        entity.UpdatedDate = now;
        return Saved(entity.MaterialId, entity.ExternalId, now);
    }
}
