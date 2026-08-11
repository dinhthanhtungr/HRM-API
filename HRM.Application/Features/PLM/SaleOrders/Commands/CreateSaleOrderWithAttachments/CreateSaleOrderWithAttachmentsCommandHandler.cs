using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrder.Services;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Services;
using HRM.Domain.Enums.Attachment;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrderWithAttachments;

/// <summary>
/// Tạo SaleOrder và upload PO trong một transaction database. Chỉ Sale thường ngoài AC/HN
/// được tự động duyệt và tạo MFG sau khi upload thành công.
/// Bất kỳ bước nào lỗi đều rollback SaleOrder; file vật lý đã upload được cleanup theo cơ chế bù trừ.
/// </summary>
internal sealed class CreateSaleOrderWithAttachmentsCommandHandler
    : IRequestHandler<CreateSaleOrderWithAttachmentsCommand, OperationResult<CreateSaleOrderResultDto>>
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly IAttachmentService _attachmentService;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SaleOrderCreationService _creationService;
    private readonly SaleOrderApprovalService _approvalService;
    private readonly ILogger<CreateSaleOrderWithAttachmentsCommandHandler> _logger;

    public CreateSaleOrderWithAttachmentsCommandHandler(
        ISaleOrderDbContext dbContext,
        IAttachmentService attachmentService,
        IFileStorage fileStorage,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        SaleOrderCreationService creationService,
        SaleOrderApprovalService approvalService,
        ILogger<CreateSaleOrderWithAttachmentsCommandHandler> logger)
    {
        _dbContext = dbContext;
        _attachmentService = attachmentService;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _creationService = creationService;
        _approvalService = approvalService;
        _logger = logger;
    }

    public async Task<OperationResult<CreateSaleOrderResultDto>> Handle(
        CreateSaleOrderWithAttachmentsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Files.Count == 0)
        {
            return OperationResult<CreateSaleOrderResultDto>.Fail(
                "Luồng tạo đơn kèm PO phải có ít nhất một tệp đính kèm.");
        }

        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        var autoApprove = SaleOrderApprovalRules.CanAutoApproveOnCreate(_currentUser);
        var now = _dateTimeProvider.Now;
        IReadOnlyList<AttachmentDto> uploadedAttachments = Array.Empty<AttachmentDto>();
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        try
        {
            var draftRequest = command.Request with { AttachmentCollectionId = Guid.Empty };
            var createResult = await _creationService.CreateAsync(
                draftRequest,
                employeeId,
                companyId,
                now,
                cancellationToken);
            if (!createResult.Success || createResult.Data is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return OperationResult<CreateSaleOrderResultDto>.Fail(
                    createResult.Message ?? "Không thể tạo SaleOrder.");
            }

            var order = createResult.Data;

            // AttachmentService kiểm tra collection trong database, nên flush vào transaction trước khi upload.
            await _dbContext.SaveChangesAsync(cancellationToken);
            uploadedAttachments = await _attachmentService.UploadListAsync(
                order.AttachmentCollectionId,
                AttachmentSlot.PurchaseOrder,
                command.Files,
                employeeId,
                cancellationToken);
            if (uploadedAttachments.Count != command.Files.Count)
            {
                return await FailAndRollbackAsync(
                    uploadedAttachments,
                    transaction,
                    "Số tệp được lưu không khớp số tệp gửi lên.",
                    cancellationToken);
            }

            if (autoApprove)
            {
                var approvalResult = await _approvalService.ApproveAsync(
                    order,
                    employeeId,
                    now,
                    cancellationToken);
                if (!approvalResult.Success)
                {
                    return await FailAndRollbackAsync(
                        uploadedAttachments,
                        transaction,
                        approvalResult.Message ?? "Không thể tự động duyệt đơn hàng.",
                        cancellationToken);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return OperationResult<CreateSaleOrderResultDto>.Ok(
                SaleOrderCreationService.ToResultDto(order),
                autoApprove
                    ? "Tạo đơn, tải PO và tự động duyệt đơn thành công."
                    : "Tạo đơn và tải PO thành công; đơn đang chờ duyệt.");
        }
        catch (OperationCanceledException)
        {
            await FailAndRollbackAsync(
                uploadedAttachments,
                transaction,
                "Thao tác tạo đơn đã bị hủy.",
                CancellationToken.None);
            throw;
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Không thể tạo SaleOrder kèm attachment.");
            return await FailAndRollbackAsync(
                uploadedAttachments,
                transaction,
                exception.Message,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Lỗi ngoài dự kiến khi tạo SaleOrder kèm attachment.");
            return await FailAndRollbackAsync(
                uploadedAttachments,
                transaction,
                "Không thể tạo đơn hàng. Không có dữ liệu SaleOrder nào được lưu.",
                CancellationToken.None);
        }
    }

    private async Task<OperationResult<CreateSaleOrderResultDto>> FailAndRollbackAsync(
        IReadOnlyList<AttachmentDto> uploadedAttachments,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        string message,
        CancellationToken cancellationToken)
    {
        var attachmentIds = uploadedAttachments.Select(x => x.AttachmentId).ToArray();
        var storagePaths = Array.Empty<string>();
        if (attachmentIds.Length > 0)
        {
            try
            {
                storagePaths = await _dbContext.AttachmentModels
                    .AsNoTracking()
                    .Where(x => attachmentIds.Contains(x.AttachmentId))
                    .Select(x => x.StoragePath)
                    .ToArrayAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Không thể đọc storage path để cleanup attachment.");
            }
        }

        foreach (var storagePath in storagePaths)
        {
            try
            {
                await _fileStorage.DeleteAsync(storagePath, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Không thể cleanup file {StoragePath} sau khi tạo SaleOrder thất bại.",
                    storagePath);
            }
        }

        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không thể rollback transaction tạo SaleOrder.");
        }

        return OperationResult<CreateSaleOrderResultDto>.Fail(message);
    }
}
