using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Attachment;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

/// <summary>Xử lý tạo và PATCH IQC: kiểm tra quyền/company/employee, attachment, cập nhật loại dòng phiếu và đồng bộ PO trong transaction.</summary>
internal sealed class IqcWriter(
    IIqcDbContext context,
    ICurrentUser currentUser,
    KeyedMutationLock<string> mutationLock,
    ICurrentUserPermissionService permissions,
    IqcEvaluationService evaluations)
{
    private async Task<WarehouseVoucherDetail?> FindDetailAsync(long id, Guid companyId, CancellationToken ct) =>
        await context.WarehouseVoucherDetails
            .FirstOrDefaultAsync(x => x.VoucherDetailId == id && x.Voucher.CompanyId == companyId, ct);

    private async Task<bool> EmployeeInCompanyAsync(Guid employeeId, Guid companyId, CancellationToken ct) =>
        await context.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId && x.CompanyId == companyId, ct);

    private async Task<bool> CanUseCollectionAsync(Guid? requested, Guid? existing, Guid companyId, CancellationToken ct)
    {
        if (!requested.HasValue || requested == Guid.Empty || requested == existing) return true;
        // Collection has no CompanyId. Prove ownership through an existing IQC parent;
        // an arbitrary/orphan collection ID cannot establish permission.
        return await context.QCInputByQCs.AsNoTracking().AnyAsync(x =>
            x.AttachmentCollectionId == requested && x.WarehouseVoucherDetail.Voucher.CompanyId == companyId, ct)
            && !await context.QCInputByQCs.AsNoTracking().AnyAsync(x =>
                x.AttachmentCollectionId == requested && x.WarehouseVoucherDetail.Voucher.CompanyId != companyId, ct);
    }

    private void EnsureCollection(QCInputByQC entity)
    {
        if (entity.AttachmentCollectionId is { } id && id != Guid.Empty) return;
        var collection = new AttachmentCollection { AttachmentCollectionId = Guid.CreateVersion7() };
        context.AttachmentCollections.Add(collection);
        entity.AttachmentCollectionId = collection.AttachmentCollectionId;
    }

    private void ApplyAttachments(QCInputByQC entity, Guid? collectionId, bool? hasNewAttachments)
    {
        if (collectionId.HasValue && collectionId != Guid.Empty) entity.AttachmentCollectionId = collectionId;
        if (hasNewAttachments != true) return;
        entity.AttachmentStatus = AttachmentUploadStatus.Pending;
        entity.AttachmentLastError = null;
        EnsureCollection(entity);
    }

    public async Task<OperationResult<GetQCInputByQC>> CreateAsync(PostQCInputByQC input, CancellationToken ct)
    {
        if (!permissions.HasPermission(ApplicationPermissions.DevAndQA.ManageIqc))
            return OperationResult<GetQCInputByQC>.Fail("Bạn không có quyền sửa IQC.");
        if (IqcRules.Company(currentUser) is not { } companyId)
            return OperationResult<GetQCInputByQC>.Fail("Không lấy được công ty hiện tại.");
        if (input is null || input.VoucherDetailId <= 0)
            return OperationResult<GetQCInputByQC>.Fail("VoucherDetailId không hợp lệ.");
        if (currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            return OperationResult<GetQCInputByQC>.Fail("Không lấy được thông tin người dùng hiện tại.");
        if (!await EmployeeInCompanyAsync(employeeId, companyId, ct))
            return OperationResult<GetQCInputByQC>.Fail("Nhân viên không thuộc công ty hiện tại.");

        using var guard = await mutationLock.AcquireAsync($"iqc:{companyId}", ct);
        await using var transaction = await context.BeginIqcTransactionAsync(ct);
        var detail = await FindDetailAsync(input.VoucherDetailId, companyId, ct);
        if (detail is null)
            return OperationResult<GetQCInputByQC>.Fail("Không tìm thấy WarehouseVoucherDetail trong công ty hiện tại.");
        var existing = await new IqcReader(context, companyId).GetLatestByVoucherDetailIdAsync(detail.VoucherDetailId, ct);
        if (existing is not null)
        {
            // Idempotent legacy POST: ignore incoming values, resynchronize PO, return stored QC.
            await new IqcPurchaseOrderSync(context).SyncAsync(detail, existing.ImportWarehouseType, companyId, employeeId, ct);
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<GetQCInputByQC>.Ok(IqcRules.ToMutationDto(existing));
        }
        var error = IqcRules.ValidateDecision(input.ImportWarehouseType);
        if (error is not null) return OperationResult<GetQCInputByQC>.Fail(error);
        if (!await CanUseCollectionAsync(input.AttachmentCollectionId, null, companyId, ct))
            return OperationResult<GetQCInputByQC>.Fail("Không có quyền sử dụng attachment collection này.");
        var entity = new QCInputByQC
        {
            QCInputByQCId = Guid.CreateVersion7(),
            VoucherDetailId = detail.VoucherDetailId,
            CSName = input.CSName,
            CSExternalId = input.CSExternalId,
            MaterialName = detail.ProductName,
            MaterialExternalId = detail.ProductCode,
            InspectionMethod = input.InspectionMethod,
            IsCOAProvided = input.IsCOAProvided,
            IsMSDSTDSProvided = input.IsMSDSTDSProvided,
            IsMetalDetectionRequired = input.IsMetalDetectionRequired,
            IsSuccessQuality = input.IsSuccessQuality,
            ImportWarehouseType = input.ImportWarehouseType,
            Note = input.Note,
            AttachmentCollectionId = input.AttachmentCollectionId,
            AttachmentStatus = input.HasNewAttachments == true ? AttachmentUploadStatus.Pending : AttachmentUploadStatus.None,
            CreatedBy = employeeId,
            CreatedDate = DateTime.Now
        };
        try
        {
            if (input.Evaluation?.Results is { Count: > 0 })
                await evaluations.StageIqcSaveAsync(entity, input.Evaluation, companyId, ct);
        }
        catch (EvaluationRequestException ex)
        {
            return OperationResult<GetQCInputByQC>.Fail(ex.Message);
        }
        EnsureCollection(entity);
        context.QCInputByQCs.Add(entity);
        if (entity.ImportWarehouseType.HasValue) detail.VoucherType = IqcRules.ToVoucherType(entity.ImportWarehouseType.Value);
        // Source sums before first save and substitutes currentDecision only when the DB row has no decision.
        await new IqcPurchaseOrderSync(context).SyncAsync(detail, entity.ImportWarehouseType, companyId, employeeId, ct);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult<GetQCInputByQC>.Ok(IqcRules.ToMutationDto(entity));
    }

    public async Task<OperationResult<GetQCInputByQC>> PatchAsync(long voucherDetailId, PatchQCInputByQC input, CancellationToken ct)
    {
        if (!permissions.HasPermission(ApplicationPermissions.DevAndQA.ManageIqc))
            return OperationResult<GetQCInputByQC>.Fail("Bạn không có quyền sửa IQC.");
        if (IqcRules.Company(currentUser) is not { } companyId)
            return OperationResult<GetQCInputByQC>.Fail("Không lấy được công ty hiện tại.");
        if (input is null || voucherDetailId <= 0)
            return OperationResult<GetQCInputByQC>.Fail("VoucherDetailId không hợp lệ.");
        if (currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            return OperationResult<GetQCInputByQC>.Fail("Không lấy được thông tin người dùng hiện tại.");
        var error = IqcMutation.Validate(input);
        if (error is not null) return OperationResult<GetQCInputByQC>.Fail(error);
        if (!await EmployeeInCompanyAsync(employeeId, companyId, ct))
            return OperationResult<GetQCInputByQC>.Fail("Nhân viên không thuộc công ty hiện tại.");

        using var guard = await mutationLock.AcquireAsync($"iqc:{companyId}", ct);
        await using var transaction = await context.BeginIqcTransactionAsync(ct);
        var detail = await FindDetailAsync(voucherDetailId, companyId, ct);
        if (detail is null)
            return OperationResult<GetQCInputByQC>.Fail("Không tìm thấy WarehouseVoucherDetail trong công ty hiện tại.");
        // Legacy PATCH selects the first QC row rather than latest. No new duplicate cleanup is performed.
        var entity = await context.QCInputByQCs.FirstOrDefaultAsync(x =>
            x.VoucherDetailId == voucherDetailId && x.WarehouseVoucherDetail.Voucher.CompanyId == companyId, ct);
        if (entity is null)
            return OperationResult<GetQCInputByQC>.Fail("Không tìm thấy dữ liệu QC để cập nhật.");
        if (!await CanUseCollectionAsync(input.AttachmentCollectionId, entity.AttachmentCollectionId, companyId, ct))
            return OperationResult<GetQCInputByQC>.Fail("Không có quyền sử dụng attachment collection này.");
        try
        {
            if (input.Evaluation?.Results is { Count: > 0 })
                await evaluations.StageIqcSaveAsync(entity, input.Evaluation, companyId, ct);
        }
        catch (EvaluationRequestException ex)
        {
            return OperationResult<GetQCInputByQC>.Fail(ex.Message);
        }
        var previousDecision = entity.ImportWarehouseType;
        IqcMutation.Apply(entity, input);
        ApplyAttachments(entity, input.AttachmentCollectionId, input.HasNewAttachments);
        if (entity.ImportWarehouseType != previousDecision && entity.ImportWarehouseType.HasValue)
            detail.VoucherType = IqcRules.ToVoucherType(entity.ImportWarehouseType.Value);
        // Preserve the source ordering: save the changed QC before recalculating accepted quantities.
        await context.SaveChangesAsync(ct);
        await new IqcPurchaseOrderSync(context).SyncAsync(detail, entity.ImportWarehouseType, companyId, employeeId, ct);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult<GetQCInputByQC>.Ok(IqcRules.ToMutationDto(entity, includeCreatedDate: true));
    }
}
