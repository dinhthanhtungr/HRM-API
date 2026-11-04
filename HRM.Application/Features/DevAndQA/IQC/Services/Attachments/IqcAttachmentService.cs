using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Attachment;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services.Attachments;

/// <summary>Gắn file vào QC mới nhất của dòng phiếu; kiểm tra capability, company và collection trước khi dùng storage chung.</summary>
internal sealed class IqcAttachmentService(
    IIqcDbContext context,
    ICurrentUser currentUser,
    ICurrentUserPermissionService permissions,
    IAttachmentService attachments,
    KeyedMutationLock<string> mutationLock) : IIqcAttachmentService
{
    public async Task<IReadOnlyList<AttachmentDto>> UploadAsync(long voucherDetailId, AttachmentSlot slot,
        IReadOnlyList<AttachmentUploadFile> files, CancellationToken ct)
    {
        var companyId = Authorize(true);
        ValidateSlot(slot);
        ValidateFiles(files);
        var employeeId = await RequireEmployeeAsync(companyId, ct);
        using var guard = await mutationLock.AcquireAsync($"iqc:{companyId}", ct);
        var qc = await FindQcAsync(voucherDetailId, companyId, ct)
            ?? throw new InvalidOperationException("Lưu QC trước khi upload ảnh/file.");
        if (qc.AttachmentCollectionId is null || qc.AttachmentCollectionId == Guid.Empty)
        {
            var collection = new AttachmentCollection { AttachmentCollectionId = Guid.CreateVersion7() };
            context.AttachmentCollections.Add(collection);
            qc.AttachmentCollectionId = collection.AttachmentCollectionId;
            await context.SaveChangesAsync(ct);
        }
        var result = await attachments.UploadListAsync(qc.AttachmentCollectionId.Value, slot, files, employeeId, ct);
        qc.AttachmentStatus = AttachmentUploadStatus.Ok;
        qc.AttachmentLastError = null;
        await context.SaveChangesAsync(ct);
        return BindUrls(voucherDetailId, result);
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(long voucherDetailId, AttachmentSlot? slot, CancellationToken ct)
    {
        var companyId = Authorize(false);
        if (slot.HasValue) ValidateSlot(slot.Value);
        var qc = await FindQcAsync(voucherDetailId, companyId, ct);
        if (qc?.AttachmentCollectionId is not { } collectionId || collectionId == Guid.Empty) return [];
        var result = await attachments.ListAsync(collectionId, slot, ct);
        return BindUrls(voucherDetailId, result.Where(x => IsAllowedSlot(x.Slot)).ToArray());
    }

    public async Task<AttachmentContent> GetContentAsync(long voucherDetailId, Guid attachmentId, CancellationToken ct)
    {
        await RequireAttachmentAsync(voucherDetailId, attachmentId, Authorize(false), ct);
        return await attachments.GetContentAsync(attachmentId, ct);
    }

    public async Task DeleteAsync(long voucherDetailId, Guid attachmentId, CancellationToken ct)
    {
        var companyId = Authorize(true);
        await RequireEmployeeAsync(companyId, ct);
        using var guard = await mutationLock.AcquireAsync($"iqc:{companyId}", ct);
        var qc = await RequireAttachmentAsync(voucherDetailId, attachmentId, companyId, ct);
        await attachments.DeleteAsync(attachmentId, ct);
        var remaining = await attachments.ListAsync(qc.AttachmentCollectionId!.Value, null, ct);
        qc.AttachmentStatus = remaining.Any(x => IsAllowedSlot(x.Slot)) ? AttachmentUploadStatus.Ok : AttachmentUploadStatus.None;
        qc.AttachmentLastError = null;
        await context.SaveChangesAsync(ct);
    }

    private Guid Authorize(bool mutation)
    {
        if (IqcRules.Company(currentUser) is not { } companyId)
            throw new UnauthorizedAccessException("Không lấy được công ty hiện tại.");
        var capability = mutation ? ApplicationPermissions.DevAndQA.ManageIqc : ApplicationPermissions.DevAndQA.ViewIqc;
        if (!permissions.HasPermission(capability)) throw new UnauthorizedAccessException("Không có quyền thao tác file IQC.");
        return companyId;
    }

    private async Task<Guid> RequireEmployeeAsync(Guid companyId, CancellationToken ct)
    {
        if (currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty ||
            !await context.Employees.AnyAsync(x => x.EmployeeId == employeeId && x.CompanyId == companyId, ct))
            throw new UnauthorizedAccessException("Nhân viên không thuộc công ty hiện tại.");
        return employeeId;
    }

    private async Task<QCInputByQC?> FindQcAsync(long voucherDetailId, Guid companyId, CancellationToken ct)
    {
        if (voucherDetailId <= 0) throw new InvalidOperationException("VoucherDetailId không hợp lệ.");
        if (!await context.WarehouseVoucherDetails.AnyAsync(x => x.VoucherDetailId == voucherDetailId && x.Voucher.CompanyId == companyId, ct))
            throw new KeyNotFoundException("Không tìm thấy dòng phiếu IQC.");
        var qc = await context.QCInputByQCs
            .Where(x => x.VoucherDetailId == voucherDetailId && x.WarehouseVoucherDetail.Voucher.CompanyId == companyId)
            .OrderByDescending(x => x.CreatedDate).FirstOrDefaultAsync(ct);
        if (qc?.AttachmentCollectionId is { } collectionId &&
            (await context.QCInputByQCs.AnyAsync(x => x.AttachmentCollectionId == collectionId &&
                x.WarehouseVoucherDetail.Voucher.CompanyId != companyId, ct) ||
             await context.AttachmentModels.AnyAsync(x => x.AttachmentCollectionId == collectionId &&
                (x.Slot == AttachmentSlot.InternalMail || x.InternalMessageAttachments.Any()), ct)))
            throw new UnauthorizedAccessException("Collection không thuộc phạm vi file IQC được phép.");
        return qc;
    }

    private async Task<QCInputByQC> RequireAttachmentAsync(long voucherDetailId, Guid attachmentId, Guid companyId, CancellationToken ct)
    {
        var qc = await FindQcAsync(voucherDetailId, companyId, ct);
        if (attachmentId == Guid.Empty || qc?.AttachmentCollectionId is not { } collectionId ||
            !await context.AttachmentModels.AsNoTracking().AnyAsync(x => x.AttachmentId == attachmentId &&
                x.AttachmentCollectionId == collectionId && x.IsActive &&
                (x.Slot == AttachmentSlot.QcReport || x.Slot == AttachmentSlot.Photo ||
                 x.Slot == AttachmentSlot.Specification || x.Slot == AttachmentSlot.Other), ct))
            throw new FileNotFoundException("Không tìm thấy file trong IQC này.");
        return qc;
    }

    private static bool IsAllowedSlot(AttachmentSlot slot) => slot is
        AttachmentSlot.QcReport or AttachmentSlot.Photo or AttachmentSlot.Specification or AttachmentSlot.Other;

    private static void ValidateSlot(AttachmentSlot slot)
    {
        if (!IsAllowedSlot(slot)) throw new InvalidOperationException("Slot file IQC không hợp lệ.");
    }

    private static void ValidateFiles(IReadOnlyList<AttachmentUploadFile> files)
    {
        if (files.Count == 0) throw new InvalidOperationException("Chưa chọn file.");
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var valid = extension switch
            {
                ".jpg" or ".jpeg" => file.ContentType == "image/jpeg",
                ".png" => file.ContentType == "image/png",
                ".webp" => file.ContentType == "image/webp",
                ".gif" => file.ContentType == "image/gif",
                ".pdf" => file.ContentType == "application/pdf",
                ".txt" or ".csv" => file.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase),
                ".doc" => file.ContentType == "application/msword",
                ".docx" => file.ContentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => file.ContentType == "application/vnd.ms-excel",
                ".xlsx" => file.ContentType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => false
            };
            if (!valid || file.Length <= 0) throw new InvalidOperationException("File rỗng hoặc extension/MIME không được hỗ trợ.");
        }
        // Storage service tiếp tục kiểm tra MIME và giới hạn dung lượng theo slot trước khi ghi file.
    }

    private static IReadOnlyList<AttachmentDto> BindUrls(long voucherDetailId, IReadOnlyList<AttachmentDto> files)
    {
        foreach (var file in files)
        {
            var route = $"/api/v1/dev-and-qa/iqc/{voucherDetailId}/attachments/{file.AttachmentId}";
            file.Url = route + "/content";
            file.DownloadUrl = route + "/download";
        }
        return files;
    }
}
