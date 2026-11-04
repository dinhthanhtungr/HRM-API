using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.PLM.Materials.Dtos.Preview;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Attachment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal sealed class MaterialAttachmentService(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IDateTimeProvider clock, IAttachmentService attachments,
    IFileStorage storage, ILogger<MaterialAttachmentService> logger) : IMaterialAttachmentService
{
    private Guid RequireCatalog()
    {
        if (!user.IsAuthenticated || !permissions.HasPermission(ApplicationPermissions.Purchasing.ManageCatalog) ||
            user.CompanyId is not { } company || company == Guid.Empty)
            throw new UnauthorizedAccessException("Không có quyền quản lý tệp NVL.");
        return company;
    }

    private async Task<Material> MaterialAsync(Guid id, Guid company, bool tracking, CancellationToken ct)
    {
        var query = tracking ? db.Materials.AsQueryable() : db.Materials.AsNoTracking();
        var material = await query.FirstOrDefaultAsync(x => x.MaterialId == id && x.CompanyId == company && x.IsActive == true, ct)
            ?? throw new FileNotFoundException("Không tìm thấy NVL active.");
        if (material.AttachmentCollectionId is { } collection &&
            await db.Materials.AnyAsync(x => x.CompanyId != company && x.AttachmentCollectionId == collection, ct))
            throw new FileNotFoundException("Không tìm thấy bộ tệp trong phạm vi NVL.");
        return material;
    }

    private async Task<Guid> WriterAsync(Guid company, CancellationToken ct)
    {
        if (user.EmployeeId is not { } employee || employee == Guid.Empty ||
            !await db.Employees.AnyAsync(x => x.EmployeeId == employee && x.CompanyId == company && x.IsActive == true, ct))
            throw new UnauthorizedAccessException("Nhân viên không thuộc công ty hoặc đã ngừng hoạt động.");
        return employee;
    }

    public async Task<MaterialAttachmentListDto> ListAsync(Guid materialId, CancellationToken ct)
        => await ListAsync(await MaterialAsync(materialId, RequireCatalog(), false, ct), ct);

    private async Task<MaterialAttachmentListDto> ListAsync(Material material, CancellationToken ct)
    {
        var rows = material.AttachmentCollectionId is not { } collection ? [] :
            await db.AttachmentModels.AsNoTracking().Where(x => x.AttachmentCollectionId == collection && x.IsActive)
                .OrderBy(x => x.CreateDate).ThenBy(x => x.AttachmentId).Select(x => new
                { x.AttachmentId, x.Slot, x.FileName, x.SizeBytes, x.CreateDate }).ToListAsync(ct);
        return new(material.MaterialId, material.UpdatedDate, rows.Select(x =>
        {
            var url = $"/api/v1/purchasing/materials/{material.MaterialId}/attachments/{x.AttachmentId}/content";
            var extension = Path.GetExtension(x.FileName).ToLowerInvariant();
            return new MaterialPreviewAttachmentDto
            {
                AttachmentId = x.AttachmentId, Slot = x.Slot, FileName = x.FileName, SizeBytes = x.SizeBytes,
                CreatedDate = x.CreateDate, IsImage = extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp",
                IsPdf = extension == ".pdf", ContentUrl = url, DownloadUrl = url + "?mode=download"
            };
        }).ToList());
    }

    private async Task<AttachmentModel> FileAsync(Material material, Guid id, CancellationToken ct)
        => await db.AttachmentModels.FirstOrDefaultAsync(x => x.AttachmentId == id && x.IsActive &&
            x.AttachmentCollectionId == material.AttachmentCollectionId, ct)
            ?? throw new FileNotFoundException("Tệp không thuộc NVL hoặc đã xóa.");

    public async Task<AttachmentContent> ContentAsync(Guid materialId, Guid attachmentId, CancellationToken ct)
    {
        var material = await MaterialAsync(materialId, RequireCatalog(), false, ct);
        await FileAsync(material, attachmentId, ct);
        return await attachments.GetContentAsync(attachmentId, ct);
    }

    public async Task<MaterialAttachmentListDto> UploadAsync(Guid materialId, AttachmentSlot slot,
        IReadOnlyList<AttachmentUploadFile> files, DateTime? expectedUpdatedDate, CancellationToken ct)
    {
        var company = RequireCatalog();
        var employee = await WriterAsync(company, ct);
        await MaterialAttachmentUploadRules.ValidateAsync(slot, files, ct);
        var savedPaths = new List<string>();
        await using var tx = await db.BeginTransactionAsync(ct);
        try
        {
            await db.LockPurchasingCatalogAsync(company, ct);
            var material = await MaterialAsync(materialId, company, true, ct);
            CheckVersion(material, expectedUpdatedDate);
            var collection = material.AttachmentCollectionId is { } existing && existing != Guid.Empty
                ? existing : Guid.CreateVersion7();
            // Reject shared collections rather than mutate files belonging to another parent.
            if (await db.Materials.AnyAsync(x => x.MaterialId != materialId && x.AttachmentCollectionId == collection, ct))
                throw new InvalidOperationException("Bộ tệp đang được dùng chung; cần kiểm tra dữ liệu trước khi sửa.");
            if (!await db.AttachmentCollections.AnyAsync(x => x.AttachmentCollectionId == collection, ct))
                db.AttachmentCollections.Add(new AttachmentCollection { AttachmentCollectionId = collection });
            material.AttachmentCollectionId = collection;
            Stamp(material, employee);
            await db.SaveChangesAsync(ct);
            foreach (var file in files)
            {
                var path = await storage.SaveAsync(file.Stream, file.ContentType, file.FileName,
                    $"collections/{collection:N}/{slot}", ct);
                savedPaths.Add(path);
                db.AttachmentModels.Add(new AttachmentModel
                {
                    AttachmentId = Guid.CreateVersion7(), AttachmentCollectionId = collection, Slot = slot,
                    FileName = Path.GetFileName(file.FileName), SizeBytes = file.Length, StoragePath = path,
                    CreateDate = clock.Now, CreateBy = employee, IsActive = true
                });
            }
            await db.SaveChangesAsync(ct);
            var result = await ListAsync(material, ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            foreach (var path in savedPaths)
            {
                try { await storage.DeleteAsync(path, CancellationToken.None); }
                catch { logger.LogError("Không thể dọn một tệp NVL sau khi rollback upload."); }
            }
            throw;
        }
    }

    public async Task<MaterialAttachmentListDto> DeleteAsync(Guid materialId, Guid attachmentId,
        DateTime? expectedUpdatedDate, CancellationToken ct)
    {
        var company = RequireCatalog();
        var employee = await WriterAsync(company, ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.LockPurchasingCatalogAsync(company, ct);
        var material = await MaterialAsync(materialId, company, true, ct);
        CheckVersion(material, expectedUpdatedDate);
        var file = await FileAsync(material, attachmentId, ct);
        if (await db.Materials.AnyAsync(x => x.MaterialId != materialId && x.AttachmentCollectionId == material.AttachmentCollectionId, ct))
            throw new InvalidOperationException("Bộ tệp đang được dùng chung; cần kiểm tra dữ liệu trước khi sửa.");
        file.IsActive = false;
        Stamp(material, employee);
        await db.SaveChangesAsync(ct);
        var result = await ListAsync(material, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private static void CheckVersion(Material material, DateTime? expected)
    {
        if (expected != material.UpdatedDate)
            throw new DbUpdateConcurrencyException("Phiên bản NVL đã thay đổi.");
    }

    private void Stamp(Material material, Guid employee)
    {
        var ticks = clock.Now.Ticks / 10 * 10;
        if (material.UpdatedDate is { } previous && ticks <= previous.Ticks) ticks = previous.Ticks / 10 * 10 + 10;
        material.UpdatedDate = new DateTime(ticks, clock.Now.Kind);
        material.UpdatedBy = employee;
    }
}
