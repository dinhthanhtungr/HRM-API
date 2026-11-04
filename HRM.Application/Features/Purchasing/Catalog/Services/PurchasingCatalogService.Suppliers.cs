using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed partial class PurchasingCatalogService
{
    public async Task<OperationResult<CatalogSavedDto>> SaveSupplierAsync(Guid? id, CatalogSupplierWriteRequest request, CancellationToken ct)
    {
        var (companyId, employeeId) = await RequireWriteContextAsync(ct);
        var error = CatalogPatch.Validate(request, "registrationNumber", "registrationAddress", "taxNumber", "phone", "website", "note", "issueDate", "issuedPlace", "faxNumber");
        if (error is not null) return Fail(error);
        if ((!id.HasValue || request.SupplierName is not null) && string.IsNullOrWhiteSpace(request.SupplierName))
            return Fail("Tên nhà cung cấp không được trống.");
        if (id.HasValue && request.ExternalId is not null) return Fail("Không được đổi mã nhà cung cấp.");
        if (!id.HasValue && request.ExpectedUpdatedDate.HasValue) return Fail("Tạo mới không nhận phiên bản cập nhật.");
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.LockPurchasingCatalogAsync(companyId, ct);
        var entity = id.HasValue ? await db.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id && x.CompanyId == companyId, ct) : null;
        if (id.HasValue && (entity is null || entity.IsActive != true)) return Fail("Không tìm thấy NCC active. Hãy khôi phục trước khi sửa.");
        if (entity is not null && CatalogPatch.Conflict(request.ExpectedUpdatedDate, entity.UpdatedDate)) return Fail(ConflictMessage);
        var now = NextUpdate(entity?.UpdatedDate);
        if (entity is null)
        {
            if (string.IsNullOrWhiteSpace(request.ExternalId))
                await db.EnsurePurchasingCatalogCounterAsync("NCC", false, ct);
            var code = string.IsNullOrWhiteSpace(request.ExternalId) ? await codes.GenerateGlobalCodeAsync(companyId, "NCC", ct) : request.ExternalId.Trim();
            var normalized = code.ToLowerInvariant();
            if (await db.Suppliers.AnyAsync(x => x.CompanyId == companyId && x.ExternalId != null && x.ExternalId.ToLower() == normalized, ct))
                return Fail("Mã NCC đã tồn tại, kể cả trong lịch sử ngừng hoạt động.");
            entity = new Supplier { SupplierId = Guid.CreateVersion7(), CompanyId = companyId, ExternalId = code,
                IsActive = true, CreatedBy = employeeId, CreatedDate = now, Priority = SupplierPriority.Medium };
            await db.Suppliers.AddAsync(entity, ct);
        }
        CatalogPatch.Text(request.SupplierName, request, nameof(request.SupplierName), () => entity.SupplierName, value => entity.SupplierName = value);
        CatalogPatch.Text(request.RegistrationNumber, request, nameof(request.RegistrationNumber), () => entity.RegistrationNumber, value => entity.RegistrationNumber = value);
        CatalogPatch.Text(request.RegistrationAddress, request, nameof(request.RegistrationAddress), () => entity.RegistrationAddress, value => entity.RegistrationAddress = value);
        CatalogPatch.Text(request.TaxNumber, request, nameof(request.TaxNumber), () => entity.TaxNumber, value => entity.TaxNumber = value);
        CatalogPatch.Text(request.Phone, request, nameof(request.Phone), () => entity.Phone, value => entity.Phone = value);
        CatalogPatch.Text(request.Website, request, nameof(request.Website), () => entity.Website, value => entity.Website = value);
        CatalogPatch.Text(request.Note, request, nameof(request.Note), () => entity.Note, value => entity.Note = value);
        CatalogPatch.Text(request.IssuedPlace, request, nameof(request.IssuedPlace), () => entity.IssuedPlace, value => entity.IssuedPlace = value);
        CatalogPatch.Text(request.FaxNumber, request, nameof(request.FaxNumber), () => entity.FaxNumber, value => entity.FaxNumber = value);
        CatalogPatch.Number(request.IssueDate, request, nameof(request.IssueDate), () => entity.IssueDate, value => entity.IssueDate = value);
        if (request.Priority.HasValue) entity.Priority = request.Priority.Value;
        entity.UpdatedBy = employeeId;
        entity.UpdatedDate = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Saved(entity.SupplierId, entity.ExternalId, now);
    }
}
