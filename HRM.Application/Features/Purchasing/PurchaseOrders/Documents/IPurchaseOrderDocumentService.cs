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

public interface IPurchaseOrderDocumentService
{
    Task<OperationResult<IReadOnlyList<PurchaseOrderDocumentDto>>> GetListAsync(
        Guid purchaseOrderId,
        PurchaseOrderDocumentType? documentType,
        bool includeInactive = false,
        CancellationToken ct = default);

    Task<OperationResult<PurchaseOrderDocumentDto>> GetByIdAsync(
        Guid purchaseOrderId,
        Guid documentId,
        CancellationToken ct = default);

    Task<OperationResult<PurchaseOrderDocumentDto>> CreateAsync(
        Guid purchaseOrderId,
        CreatePurchaseOrderDocumentRequest request,
        CancellationToken ct = default);

    Task<OperationResult<PurchaseOrderDocumentDto>> UpdateAsync(
        Guid purchaseOrderId,
        Guid documentId,
        UpdatePurchaseOrderDocumentRequest request,
        CancellationToken ct = default);

    Task<OperationResult> DeleteAsync(
        Guid purchaseOrderId,
        Guid documentId,
        CancellationToken ct = default);

    Task<OperationResult<IReadOnlyList<AttachmentDto>>> GetAttachmentsAsync(
        Guid purchaseOrderId,
        Guid documentId,
        PurchaseOrderDocumentType? slot,
        CancellationToken ct = default);

    Task<OperationResult<IReadOnlyList<AttachmentDto>>> UploadAttachmentsAsync(
        Guid purchaseOrderId,
        Guid documentId,
        IReadOnlyList<AttachmentUploadFile> files,
        CancellationToken ct = default);

    Task<OperationResult<PurchaseOrderDocumentDto>> UploadAttachmentsByTypeAsync(
        Guid purchaseOrderId,
        PurchaseOrderDocumentType documentType,
        IReadOnlyList<AttachmentUploadFile> files,
        CancellationToken ct = default);

    Task<OperationResult<AttachmentContent>> GetAttachmentContentAsync(
        Guid purchaseOrderId,
        Guid documentId,
        Guid attachmentId,
        CancellationToken ct = default);

    Task<OperationResult> DeleteAttachmentAsync(
        Guid purchaseOrderId,
        Guid documentId,
        Guid attachmentId,
        CancellationToken ct = default);

    Task<OperationResult> HardDeleteAttachmentAsync(
        Guid purchaseOrderId,
        Guid documentId,
        Guid attachmentId,
        CancellationToken ct = default);
}
