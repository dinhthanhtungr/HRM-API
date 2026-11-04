using HRM.Domain.Enums.Orders;
using HRM.Application.Commons.Authorization;
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

internal sealed partial class PurchaseOrderDocumentService(
    IPurchaseOrderDbContext db, IAttachmentService attachments, ICurrentUser user,
    ICurrentUserPermissionService permissions) : IPurchaseOrderDocumentService
{
    public async Task<OperationResult<IReadOnlyList<PurchaseOrderDocumentDto>>> GetListAsync(
        Guid purchaseOrderId,
        PurchaseOrderDocumentType? documentType,
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        Authorize();
        var exists = await PurchaseOrderExistsAsync(purchaseOrderId, ct);
        if (!exists)
            return OperationResult<IReadOnlyList<PurchaseOrderDocumentDto>>.Fail("Không tìm thấy đơn mua hàng.");

        if (documentType.HasValue) ValidateSlot(documentType.Value);
        var query = db.PurchaseOrderDocuments.AsNoTracking()
            .Where(x => x.PurchaseOrderId == purchaseOrderId);

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        if (documentType.HasValue)
            query = query.Where(x => x.DocumentType == documentType.Value);

        var rows = await query
            .OrderBy(x => x.DocumentType)
            .ThenByDescending(x => x.CreatedDate)
            .ToListAsync(ct);

        var result = new List<PurchaseOrderDocumentDto>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(await MapAsync(row, includeAttachments: true, ct));
        }

        return OperationResult<IReadOnlyList<PurchaseOrderDocumentDto>>.Ok(result);
    }

    public async Task<OperationResult<PurchaseOrderDocumentDto>> GetByIdAsync(
        Guid purchaseOrderId,
        Guid documentId,
        CancellationToken ct = default)
    {
        Authorize();
        var document = await FindDocumentAsync(purchaseOrderId, documentId, track: false, ct);
        if (document is null)
            return OperationResult<PurchaseOrderDocumentDto>.Fail("Không tìm thấy chứng từ đơn mua hàng.");

        return OperationResult<PurchaseOrderDocumentDto>.Ok(await MapAsync(document, includeAttachments: true, ct));
    }

    public async Task<OperationResult<PurchaseOrderDocumentDto>> CreateAsync(
        Guid purchaseOrderId,
        CreatePurchaseOrderDocumentRequest request,
        CancellationToken ct = default)
    {
        await using var write = await BeginEditAsync(purchaseOrderId, ct);
        var exists = await PurchaseOrderExistsAsync(purchaseOrderId, ct);
        if (!exists)
            return OperationResult<PurchaseOrderDocumentDto>.Fail("Không tìm thấy đơn mua hàng.");

        var now = DateTime.Now;
        var userId = await RequireEmployeeAsync(ct);

        ValidateSlot(request.DocumentType);
        var attachmentCollectionId = request.AttachmentCollectionId;
        if (attachmentCollectionId.HasValue)
            await EnsureAttachmentCollectionAsync(attachmentCollectionId.Value, ct);

        var document = new PurchaseOrderDocument
        {
            PurchaseOrderDocumentId = Guid.CreateVersion7(),
            PurchaseOrderId = purchaseOrderId,
            DocumentType = request.DocumentType,
            AttachmentCollectionId = attachmentCollectionId,
            DocumentCode = Normalize(request.DocumentCode),
            DocumentName = Normalize(request.DocumentName),
            Note = Normalize(request.Note),
            IsReceived = request.IsReceived,
            ReceivedDate = request.IsReceived ? request.ReceivedDate ?? now : request.ReceivedDate,
            CreatedDate = now,
            CreatedBy = userId,
            IsActive = true
        };

        await db.PurchaseOrderDocuments.AddAsync(document, ct);
        await db.SaveChangesAsync(ct);

        await write.CommitAsync(ct);
        return OperationResult<PurchaseOrderDocumentDto>.Ok(
            await MapAsync(document, includeAttachments: true, ct),
            "Đã tạo chứng từ đơn mua hàng.");
    }

    public async Task<OperationResult<PurchaseOrderDocumentDto>> UpdateAsync(
        Guid purchaseOrderId,
        Guid documentId,
        UpdatePurchaseOrderDocumentRequest request,
        CancellationToken ct = default)
    {
        await using var write = await BeginEditAsync(purchaseOrderId, ct);
        var document = await FindDocumentAsync(purchaseOrderId, documentId, track: true, ct, includeInactive: request.IsActive == true);
        if (document is null)
            return OperationResult<PurchaseOrderDocumentDto>.Fail("Không tìm thấy chứng từ đơn mua hàng.");

        var now = DateTime.Now;
        var userId = await RequireEmployeeAsync(ct);

        if (request.DocumentType.HasValue)
        {
            ValidateSlot(request.DocumentType.Value);
            document.DocumentType = request.DocumentType.Value;
        }

        if (request.AttachmentCollectionId.HasValue)
        {
            await EnsureAttachmentCollectionAsync(request.AttachmentCollectionId.Value, ct);
            document.AttachmentCollectionId = request.AttachmentCollectionId.Value;
        }

        if (request.DocumentCode is not null)
            document.DocumentCode = Normalize(request.DocumentCode);

        if (request.DocumentName is not null)
            document.DocumentName = Normalize(request.DocumentName);

        if (request.Note is not null)
            document.Note = Normalize(request.Note);

        if (request.IsReceived.HasValue)
        {
            document.IsReceived = request.IsReceived.Value;
            if (document.IsReceived && document.ReceivedDate is null)
                document.ReceivedDate = request.ReceivedDate ?? now;
            else if (!document.IsReceived && request.ReceivedDate.HasValue)
                document.ReceivedDate = request.ReceivedDate;
        }
        else if (request.ReceivedDate.HasValue)
        {
            document.ReceivedDate = request.ReceivedDate;
        }

        if (request.Verify.HasValue)
        {
            document.VerifiedBy = request.Verify.Value ? userId : null;
            document.VerifiedDate = request.Verify.Value ? now : null;
        }

        if (request.IsActive.HasValue)
            document.IsActive = request.IsActive.Value;

        document.UpdatedDate = now;
        document.UpdatedBy = userId;

        await db.SaveChangesAsync(ct);

        await write.CommitAsync(ct);
        return OperationResult<PurchaseOrderDocumentDto>.Ok(
            await MapAsync(document, includeAttachments: true, ct),
            "Đã cập nhật chứng từ đơn mua hàng.");
    }

    public async Task<OperationResult> DeleteAsync(
        Guid purchaseOrderId,
        Guid documentId,
        CancellationToken ct = default)
    {
        await using var write = await BeginEditAsync(purchaseOrderId, ct);
        var document = await FindDocumentAsync(purchaseOrderId, documentId, track: true, ct);
        if (document is null)
            return OperationResult.Fail("Không tìm thấy chứng từ đơn mua hàng.");

        document.IsActive = false;
        document.UpdatedDate = DateTime.Now;
        document.UpdatedBy = await RequireEmployeeAsync(ct);

        await db.SaveChangesAsync(ct);
        await write.CommitAsync(ct);
        return OperationResult.Ok("Đã xóa chứng từ đơn mua hàng.");
    }

}
