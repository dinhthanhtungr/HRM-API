using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Enums.Attachment;
using HRM.Domain.Enums.Devandqa;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations.Attachments;

/// <summary>Lưu dữ liệu và file trong một request: transaction DB cha, dọn file mới nếu rollback.</summary>
internal sealed partial class EvaluationFileSaveService(IEvaluationFileSaveDbContext db, ISender sender,
    EvaluationAccessService access, EvaluationFileOwnerResolver owners, IFileStorage storage,
    ILogger<EvaluationFileSaveService> logger) : IEvaluationFileSaveService
{
    public async Task<EvaluationFileSaveDto> SaveAsync(EvaluationFileResource resource, string? parentKey,
        bool create, EvaluationFileSaveEnvelope request, IReadOnlyList<EvaluationNamedUpload> uploads, CancellationToken ct)
    {
        var company = access.Company(true);
        var employee = await access.EmployeeAsync(company, ct);
        if (resource is not (EvaluationFileResource.Iqc or EvaluationFileResource.Coa or EvaluationFileResource.Lab))
            throw new EvaluationRequestException("Chỉ kết quả IQC/COA/LabTest được lưu tệp đính kèm.");
        var bindings = request.Attachments ?? [];
        ValidateManifest(bindings, uploads);
        var paths = new List<string>();
        await using var transaction = await db.BeginCombinedEvaluationSaveAsync(ct);
        try
        {
            var (record, parentId) = await SaveRecordAsync(resource, parentKey, create, request.Payload, ct);
            var rows = await owners.ResolveAsync(resource, parentId, true, ct);
            var plans = new List<(EvaluationFileOwner Owner, List<AttachmentUploadFile> Files, List<AttachmentModel> Deletes)>();
            foreach (var binding in bindings)
            {
                var owner = rows.SingleOrDefault(x => x.StandardId == binding.EvaluationStandardId)
                    ?? throw new EvaluationRequestException("File không ghép được vào chỉ tiêu đã lưu.");
                await owners.RequireExclusiveCollectionAsync(owner, ct);
                var current = owner.CollectionId is { } id
                    ? await db.AttachmentModels.Where(x => x.AttachmentCollectionId == id && x.IsActive && x.Slot == AttachmentSlot.EvaluationAttachment).ToListAsync(ct)
                    : [];
                var deleteIds = binding.DeleteAttachmentIds ?? [];
                if (deleteIds.Any(id => !current.Any(x => x.AttachmentId == id)))
                    throw new EvaluationRequestException("Không tìm thấy file cần xóa trong chỉ tiêu này.", 404);
                var files = (binding.FileKeys ?? []).Select(key => uploads.Single(x => x.Key == key).File).ToList();
                EvaluationAttachmentRules.Validate(owner.Configuration, files, current.Count - deleteIds.Count);
                plans.Add((owner, files, current.Where(x => deleteIds.Contains(x.AttachmentId)).ToList()));
            }
            foreach (var plan in plans)
            {
                foreach (var deleted in plan.Deletes) deleted.IsActive = false;
                var collectionId = plan.Owner.CollectionId;
                if (plan.Files.Count > 0 && collectionId is null)
                {
                    collectionId = Guid.CreateVersion7();
                    db.AttachmentCollections.Add(new() { AttachmentCollectionId = collectionId.Value });
                    plan.Owner.SetCollection(collectionId);
                }
                foreach (var file in plan.Files)
                {
                    var path = $"evaluation/{company:N}/{collectionId:N}/{Guid.CreateVersion7():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
                    paths.Add(path);
                    await storage.SaveAtPathAsync(file.Stream, path, ct);
                    db.AttachmentModels.Add(new()
                    {
                        AttachmentId = Guid.CreateVersion7(), AttachmentCollectionId = collectionId!.Value,
                        Slot = AttachmentSlot.EvaluationAttachment, FileName = Path.GetFileName(file.FileName),
                        SizeBytes = file.Length, StoragePath = path, CreateBy = employee, CreateDate = DateTime.Now, IsActive = true
                    });
                }
            }
            await access.SaveAsync(ct);
            // Resolve lại để lấy collection vừa được tạo; trả toàn bộ nhóm file của form.
            rows = await owners.ResolveAsync(resource, parentId, true, ct);
            var groups = await GroupsAsync(resource, parentId, rows, ct);
            foreach (var row in rows.Where(x => x.Configuration.DataType != EvaluationDataType.Attachment))
                if (groups.Single(x => x.OwnerId == row.Id).Files.Count > 0)
                    throw new EvaluationRequestException("Cần xóa file trước khi đổi chỉ tiêu sang kiểu khác Attachment.");
            foreach (var row in rows.Where(x => x.IsResult && x.Configuration.DataType == EvaluationDataType.Attachment))
                if (row.Configuration.IsRequired && groups.Single(x => x.OwnerId == row.Id).Files.Count == 0)
                    throw new EvaluationRequestException("Chỉ tiêu Attachment bắt buộc cần ít nhất một file.");
            var response = new EvaluationFileSaveDto(RefreshCollectionIds(record, groups), parentId, groups);
            await transaction.CommitAsync(ct);
            return response;
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception error)
            {
                // Không xóa file nếu không xác nhận được rollback (ví dụ mất kết nối lúc commit).
                logger.LogWarning(error, "Không xác nhận được rollback evaluation; cần kiểm tra trạng thái lưu.");
                throw;
            }
            foreach (var path in paths)
            {
                try { await storage.DeleteAsync(path, CancellationToken.None); }
                catch (Exception error) { logger.LogWarning(error, "Không dọn được file evaluation sau rollback."); }
            }
            throw;
        }
        finally { db.EndCombinedEvaluationSave(); }
    }

    private static object RefreshCollectionIds(object record, List<EvaluationFileGroupDto> groups) => record switch
    {
        HRM.Application.Features.DevAndQA.LabTests.Dtos.BasicLabTestDto lab =>
            lab with { Results = lab.Results.Select(x => x with { AttachmentCollectionId = groups.Single(g => g.OwnerId == x.Id).AttachmentCollectionId }).ToList() },
        HRM.Application.Features.DevAndQA.CoaCertificates.Dtos.CoaCertificateDto coa =>
            coa with { Results = coa.Results.Select(x => x with { AttachmentCollectionId = groups.Single(g => g.OwnerId == x.Id).AttachmentCollectionId }).ToList() },
        _ => record
    };

    internal static void ValidateManifest(IReadOnlyList<EvaluationFileBinding> bindings, IReadOnlyList<EvaluationNamedUpload> uploads)
    {
        if (uploads.Count > 200 || uploads.Any(x => x.File.Length <= 0) || uploads.Sum(x => x.File.Length) > 50_000_000)
            throw new EvaluationRequestException("File vượt giới hạn một lần lưu.");
        if (bindings.Count > 200 || bindings.Any(x => x is null) || bindings.Select(x => x.EvaluationStandardId).Distinct().Count() != bindings.Count)
            throw new EvaluationRequestException("Danh sách ghép file không hợp lệ hoặc trùng chỉ tiêu.");
        var keys = bindings.SelectMany(x => x.FileKeys ?? []).ToArray();
        if (keys.Length > 200 || keys.Any(string.IsNullOrWhiteSpace) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length ||
            uploads.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != uploads.Count ||
            !keys.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(uploads.Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal)))
            throw new EvaluationRequestException("Mỗi file phải có key duy nhất và được ghép đúng một lần.");
        foreach (var binding in bindings)
            if (binding.DeleteAttachmentIds is { } ids && (ids.Count > 20 || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Count))
                throw new EvaluationRequestException("Danh sách file cần xóa không hợp lệ.");
    }

    public async Task<IReadOnlyList<EvaluationFileGroupDto>> ListAsync(EvaluationFileResource resource, Guid parentId, CancellationToken ct) =>
        await GroupsAsync(resource, parentId, await owners.ResolveAsync(resource, parentId, false, ct), ct);

    private async Task<List<EvaluationFileGroupDto>> GroupsAsync(EvaluationFileResource resource, Guid parentId, List<EvaluationFileOwner> rows, CancellationToken ct)
    {
        var result = new List<EvaluationFileGroupDto>();
        foreach (var row in rows)
        {
            await owners.RequireExclusiveCollectionAsync(row, ct);
            var files = row.CollectionId is { } collection
                ? await db.AttachmentModels.AsNoTracking().Where(x => x.AttachmentCollectionId == collection && x.IsActive && x.Slot == AttachmentSlot.EvaluationAttachment).ToListAsync(ct)
                : [];
            result.Add(new(row.Id, row.StandardId, row.CollectionId, files.OrderBy(x => x.CreateDate).Select(x => new AttachmentDto
            {
                AttachmentId = x.AttachmentId, AttachmentCollectionId = x.AttachmentCollectionId, FileName = x.FileName,
                SizeBytes = x.SizeBytes, Slot = x.Slot, CreateDate = x.CreateDate,
                IsImage = x.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || x.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || x.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || x.FileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) || x.FileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase),
                Url = $"/api/v1/dev-and-qa/evaluation-attachments/{resource}/{parentId}/{row.Id}/{x.AttachmentId}/content",
                DownloadUrl = $"/api/v1/dev-and-qa/evaluation-attachments/{resource}/{parentId}/{row.Id}/{x.AttachmentId}/download"
            }).ToList(), row.Configuration));
        }
        return result;
    }

    public async Task<AttachmentContent> ContentAsync(EvaluationFileResource resource, Guid parentId, Guid ownerId, Guid attachmentId, CancellationToken ct)
    {
        var owner = (await owners.ResolveAsync(resource, parentId, false, ct)).SingleOrDefault(x => x.Id == ownerId)
            ?? throw new EvaluationRequestException("Không tìm thấy dòng chỉ tiêu.", 404);
        await owners.RequireExclusiveCollectionAsync(owner, ct);
        var file = await db.AttachmentModels.AsNoTracking().FirstOrDefaultAsync(x => x.AttachmentId == attachmentId &&
            x.AttachmentCollectionId == owner.CollectionId && x.IsActive && x.Slot == AttachmentSlot.EvaluationAttachment, ct)
            ?? throw new EvaluationRequestException("Không tìm thấy file trong chỉ tiêu này.", 404);
        var content = await storage.OpenReadAsync(file.StoragePath, ct);
        return new(content.Stream, content.ContentType, file.FileName, content.Length);
    }
}
