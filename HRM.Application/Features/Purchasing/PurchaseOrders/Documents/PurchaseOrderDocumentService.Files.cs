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
    public async Task<OperationResult<IReadOnlyList<AttachmentDto>>> GetAttachmentsAsync(
        Guid purchaseOrderId,
        Guid documentId,
        PurchaseOrderDocumentType? slot,
        CancellationToken ct = default)
    {
        Authorize();
        var document = await FindDocumentAsync(purchaseOrderId, documentId, track: false, ct);
        if (document is null)
            return OperationResult<IReadOnlyList<AttachmentDto>>.Fail("Không tìm thấy chứng từ đơn mua hàng.");

        if (slot.HasValue && slot.Value != document.DocumentType)
            return OperationResult<IReadOnlyList<AttachmentDto>>.Fail("Slot không thuộc loại chứng từ này.");

        if (!document.AttachmentCollectionId.HasValue)
            return OperationResult<IReadOnlyList<AttachmentDto>>.Ok(new List<AttachmentDto>());

        var files = await attachments.ListAsync(
            document.AttachmentCollectionId.Value,
            ToStorageSlot(document.DocumentType),
            ct);

        return OperationResult<IReadOnlyList<AttachmentDto>>.Ok(BindUrls(document, files));
    }

    public async Task<OperationResult<IReadOnlyList<AttachmentDto>>> UploadAttachmentsAsync(
        Guid purchaseOrderId,
        Guid documentId,
        IReadOnlyList<AttachmentUploadFile> files,
        CancellationToken ct = default)
    {
        await using var write = await BeginEditAsync(purchaseOrderId, ct);
        ValidateFiles(files);
        if (files is null || files.Count == 0)
            return OperationResult<IReadOnlyList<AttachmentDto>>.Fail("Chưa chọn file cần tải lên.");

        Authorize();
        var document = await FindDocumentAsync(purchaseOrderId, documentId, track: true, ct);
        if (document is null)
            return OperationResult<IReadOnlyList<AttachmentDto>>.Fail("Không tìm thấy chứng từ đơn mua hàng.");

        var now = DateTime.Now;
        var userId = await RequireEmployeeAsync(ct);

        if (!document.AttachmentCollectionId.HasValue || document.AttachmentCollectionId.Value == Guid.Empty)
        {
            document.AttachmentCollectionId = await CreateAttachmentCollectionAsync(ct);
            document.UpdatedDate = now;
            document.UpdatedBy = userId;

            await db.SaveChangesAsync(ct);
        }

        var uploaded = await attachments.UploadListAsync(
            document.AttachmentCollectionId.Value,
            ToStorageSlot(document.DocumentType),
            files,
            userId,
            ct);

        document.IsReceived = true;
        document.ReceivedDate ??= now;
        document.UpdatedDate = now;
        document.UpdatedBy = userId;

        await db.SaveChangesAsync(ct);

        await write.CommitAsync(ct);
        return OperationResult<IReadOnlyList<AttachmentDto>>.Ok(BindUrls(document, uploaded), "Đã tải chứng từ lên đơn mua hàng.");
    }

    public async Task<OperationResult<PurchaseOrderDocumentDto>> UploadAttachmentsByTypeAsync(
        Guid purchaseOrderId,
        PurchaseOrderDocumentType documentType,
        IReadOnlyList<AttachmentUploadFile> files,
        CancellationToken ct = default)
    {
        await using var write = await BeginEditAsync(purchaseOrderId, ct);
        ValidateFiles(files);
        if (files is null || files.Count == 0)
            return OperationResult<PurchaseOrderDocumentDto>.Fail("Chưa chọn file cần tải lên.");

        Authorize();
        var exists = await PurchaseOrderExistsAsync(purchaseOrderId, ct);
        if (!exists)
            return OperationResult<PurchaseOrderDocumentDto>.Fail("Không tìm thấy đơn mua hàng.");

        ValidateSlot(documentType);
        var now = DateTime.Now;
        var userId = await RequireEmployeeAsync(ct);

        var document = await db.PurchaseOrderDocuments
            .Where(x =>
                x.PurchaseOrderId == purchaseOrderId &&
                x.DocumentType == documentType &&
                x.IsActive)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync(ct);

        if (document is null)
        {
            document = new PurchaseOrderDocument
            {
                PurchaseOrderDocumentId = Guid.CreateVersion7(),
                PurchaseOrderId = purchaseOrderId,
                DocumentType = documentType,
                AttachmentCollectionId = await CreateAttachmentCollectionAsync(ct),
                DocumentName = documentType.ToString(),
                IsReceived = false,
                CreatedDate = now,
                CreatedBy = userId,
                IsActive = true
            };

            await db.PurchaseOrderDocuments.AddAsync(document, ct);
            await db.SaveChangesAsync(ct);
        }
        else if (!document.AttachmentCollectionId.HasValue || document.AttachmentCollectionId.Value == Guid.Empty)
        {
            document.AttachmentCollectionId = await CreateAttachmentCollectionAsync(ct);
            document.UpdatedDate = now;
            document.UpdatedBy = userId;

            await db.SaveChangesAsync(ct);
        }

        await attachments.UploadListAsync(
            document.AttachmentCollectionId!.Value,
            ToStorageSlot(document.DocumentType),
            files,
            userId,
            ct);

        document.IsReceived = true;
        document.ReceivedDate ??= now;
        document.UpdatedDate = now;
        document.UpdatedBy = userId;

        await db.SaveChangesAsync(ct);

        await write.CommitAsync(ct);
        return OperationResult<PurchaseOrderDocumentDto>.Ok(
            await MapAsync(document, includeAttachments: true, ct),
            "Đã tải chứng từ lên đơn mua hàng.");
    }

    public async Task<OperationResult<AttachmentContent>> GetAttachmentContentAsync(
        Guid purchaseOrderId,
        Guid documentId,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        Authorize();
        var belongs = await AttachmentBelongsToDocumentAsync(purchaseOrderId, documentId, attachmentId, ct);
        if (!belongs)
            return OperationResult<AttachmentContent>.Fail("File không thuộc chứng từ đơn mua hàng này.");

        return OperationResult<AttachmentContent>.Ok(await attachments.GetContentAsync(attachmentId, ct));
    }

    public async Task<OperationResult> DeleteAttachmentAsync(
        Guid purchaseOrderId,
        Guid documentId,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        await using var write = await BeginEditAsync(purchaseOrderId, ct);
        var belongs = await AttachmentBelongsToDocumentAsync(purchaseOrderId, documentId, attachmentId, ct);
        if (!belongs)
            return OperationResult.Fail("File không thuộc chứng từ đơn mua hàng này.");

        await RequireEmployeeAsync(ct);
        await attachments.DeleteAsync(attachmentId, ct);
        await write.CommitAsync(ct);
        return OperationResult.Ok("Đã xóa file chứng từ.");
    }

    public async Task<OperationResult> HardDeleteAttachmentAsync(
        Guid purchaseOrderId,
        Guid documentId,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        await using var write = await BeginEditAsync(purchaseOrderId, ct);
        var belongs = await AttachmentBelongsToDocumentAsync(purchaseOrderId, documentId, attachmentId, ct);
        if (!belongs)
            return OperationResult.Fail("File không thuộc chứng từ đơn mua hàng này.");

        await RequireEmployeeAsync(ct);
        await attachments.HardDeleteAsync(attachmentId, ct);
        await write.CommitAsync(ct);
        return OperationResult.Ok("Đã xóa vĩnh viễn file chứng từ.");
    }

}
