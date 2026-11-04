using HRM.Domain.Enums.Orders;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Attachment;
using Microsoft.EntityFrameworkCore;
namespace HRM.Application.Features.Purchasing.PurchaseOrders.Documents;

internal sealed partial class PurchaseOrderDocumentService
{
    private async Task<bool> PurchaseOrderExistsAsync(Guid purchaseOrderId, CancellationToken ct)
    {
        var companyId = Authorize();
        return await db.PurchaseOrders.AsNoTracking().AnyAsync(x => x.PurchaseOrderId == purchaseOrderId && x.CompanyId == companyId, ct);
    }
    private async Task<PurchaseOrderDocument?> FindDocumentAsync(Guid purchaseOrderId, Guid documentId, bool track, CancellationToken ct, bool includeInactive = false)
    {
        var companyId = Authorize();
        var query = db.PurchaseOrderDocuments.Where(x => x.PurchaseOrderDocumentId == documentId && x.PurchaseOrderId == purchaseOrderId &&
            x.PurchaseOrder.CompanyId == companyId && (includeInactive || x.IsActive));
        var document = await (track ? query : query.AsNoTracking()).FirstOrDefaultAsync(ct);
        if (document?.AttachmentCollectionId is { } id) await EnsureAttachmentCollectionAsync(id, ct);
        return document;
    }
    private async Task<PurchaseOrderDocumentDto> MapAsync(
        PurchaseOrderDocument document,
        bool includeAttachments,
        CancellationToken ct)
    {
        IReadOnlyList<AttachmentDto> files = [];
        if (includeAttachments && document.AttachmentCollectionId.HasValue)
        {
            await EnsureAttachmentCollectionAsync(document.AttachmentCollectionId.Value, ct);
            files = await attachments.ListAsync(
                document.AttachmentCollectionId.Value,
                ToStorageSlot(document.DocumentType),
                ct);
        }

        return new PurchaseOrderDocumentDto
        {
            PurchaseOrderDocumentId = document.PurchaseOrderDocumentId,
            PurchaseOrderId = document.PurchaseOrderId,
            DocumentType = document.DocumentType,
            DocumentTypeName = document.DocumentType.ToString(),
            AttachmentCollectionId = document.AttachmentCollectionId,
            DocumentCode = document.DocumentCode,
            DocumentName = document.DocumentName,
            Note = document.Note,
            IsReceived = document.IsReceived,
            ReceivedDate = document.ReceivedDate,
            VerifiedBy = document.VerifiedBy,
            VerifiedDate = document.VerifiedDate,
            CreatedDate = document.CreatedDate,
            CreatedBy = document.CreatedBy,
            UpdatedDate = document.UpdatedDate,
            UpdatedBy = document.UpdatedBy,
            IsActive = document.IsActive,
            Attachments = BindUrls(document, files)
        };
    }

    private async Task<Guid> CreateAttachmentCollectionAsync(CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        await db.AttachmentCollections.AddAsync(new AttachmentCollection
        {
            AttachmentCollectionId = id
        }, ct);

        return id;
    }

    private async Task EnsureAttachmentCollectionAsync(Guid id, CancellationToken ct)
    {
        var companyId = Authorize();
        if (id == Guid.Empty || !await db.PurchaseOrderDocuments.AnyAsync(x => x.AttachmentCollectionId == id && x.PurchaseOrder.CompanyId == companyId, ct) ||
            await db.PurchaseOrderDocuments.AnyAsync(x => x.AttachmentCollectionId == id && x.PurchaseOrder.CompanyId != companyId, ct) ||
            await db.QCInputByQCs.AnyAsync(x => x.AttachmentCollectionId == id && x.WarehouseVoucherDetail.Voucher.CompanyId != companyId, ct) ||
            await db.AttachmentModels.AnyAsync(x => x.AttachmentCollectionId == id && (x.Slot == AttachmentSlot.InternalMail || x.InternalMessageAttachments.Any()), ct))
            throw new UnauthorizedAccessException("Collection không thuộc chứng từ PO của công ty hiện tại.");
    }
    private async Task<bool> AttachmentBelongsToDocumentAsync(
        Guid purchaseOrderId,
        Guid documentId,
        Guid attachmentId,
        CancellationToken ct)
    {
        Authorize();
        var document = await FindDocumentAsync(purchaseOrderId, documentId, track: false, ct);
        if (document?.AttachmentCollectionId is null)
            return false;

        return await db.AttachmentModels.AsNoTracking()
            .AnyAsync(x =>
                x.AttachmentId == attachmentId && x.IsActive && x.Slot == ToStorageSlot(document.DocumentType) &&
                x.AttachmentCollectionId == document.AttachmentCollectionId.Value,
                ct);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private Guid Authorize()
    {
        if (!user.IsAuthenticated || !PurchaseOrderAccess.CanManage(permissions)) throw new UnauthorizedAccessException("Không có quyền quản lý chứng từ PO.");
        if (user.CompanyId is not { } company || company == Guid.Empty) throw new UnauthorizedAccessException("Không xác định được công ty hiện tại.");
        return company;
    }
    private async Task<Guid> RequireEmployeeAsync(CancellationToken ct)
    {
        var companyId = Authorize();
        if (user.EmployeeId is not { } id || id == Guid.Empty) throw new UnauthorizedAccessException("Không xác định được nhân viên hiện tại.");
        if (!await db.Employees.AnyAsync(x => x.EmployeeId == id && x.CompanyId == companyId && x.IsActive, ct)) throw new UnauthorizedAccessException("Nhân viên không thuộc công ty hiện tại.");
        return id;
    }
    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginEditAsync(Guid purchaseOrderId, CancellationToken ct)
    {
        var company = Authorize();
        await RequireEmployeeAsync(ct);
        var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            await db.LockPurchaseOrderAsync(purchaseOrderId, company, ct);
            var po = await db.PurchaseOrders.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PurchaseOrderId == purchaseOrderId && x.CompanyId == company, ct);
            if (po is null) throw new PurchaseOrderWriteException("Không tìm thấy đơn mua hàng.");
            if (!PurchaseOrderEditRules.CanEdit(po)) throw new PurchaseOrderWriteException(PurchaseOrderEditRules.LockedMessage);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    private static void ValidateSlot(PurchaseOrderDocumentType type)
    {
        if (!Enum.IsDefined(type)) throw new PurchaseOrderWriteException("Loại chứng từ PO không hợp lệ.");
    }
    internal static AttachmentSlot ToStorageSlot(PurchaseOrderDocumentType type) => type switch
    {
        PurchaseOrderDocumentType.TDS_MSDS => AttachmentSlot.MaterialTds,
        PurchaseOrderDocumentType.COA => AttachmentSlot.MaterialCoa,
        PurchaseOrderDocumentType.Acceptance => AttachmentSlot.Other,
        >= PurchaseOrderDocumentType.Contract and <= PurchaseOrderDocumentType.QcReport => (AttachmentSlot)(int)type,
        _ => throw new PurchaseOrderWriteException("Loại chứng từ PO không hợp lệ.")
    };
    private static IReadOnlyList<AttachmentDto> BindUrls(PurchaseOrderDocument document, IReadOnlyList<AttachmentDto> files)
    {
        foreach (var file in files)
        {
            var route = $"/api/v1/purchasing/purchase-orders/{document.PurchaseOrderId}/documents/{document.PurchaseOrderDocumentId}/attachments/{file.AttachmentId}/content";
            file.Url = route;
            file.DownloadUrl = route + "?mode=download";
        }
        return files;
    }
    private static void ValidateFiles(IReadOnlyList<AttachmentUploadFile> files)
    {
        if (files is null || files.Count == 0) throw new PurchaseOrderWriteException("Chưa chọn file.");
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var valid = extension switch
            {
                ".jpg" or ".jpeg" => file.ContentType == "image/jpeg",
                ".png" => file.ContentType == "image/png",
                ".webp" => file.ContentType == "image/webp",
                ".gif" => file.ContentType == "image/gif",
                ".zip" => file.ContentType == "application/zip",
                ".7z" => file.ContentType == "application/x-7z-compressed",
                ".rar" => file.ContentType == "application/x-rar-compressed",
                ".pdf" => file.ContentType == "application/pdf",
                ".txt" or ".csv" => file.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase),
                ".doc" => file.ContentType == "application/msword",
                ".docx" => file.ContentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => file.ContentType == "application/vnd.ms-excel",
                ".xlsx" => file.ContentType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => false
            };
            if (!valid || file.Length <= 0) throw new PurchaseOrderWriteException("File rỗng hoặc extension/MIME không được hỗ trợ.");
        }
        // Storage service tiếp tục kiểm tra MIME và giới hạn dung lượng theo slot trước khi ghi file.
    }

}
