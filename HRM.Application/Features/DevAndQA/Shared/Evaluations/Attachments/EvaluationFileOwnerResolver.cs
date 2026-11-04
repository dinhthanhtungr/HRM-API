using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Features.DevAndQA.CoaCertificates.Services;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations.Attachments;

/// <summary>Scope qua entity cha trước khi lấy collection; không chấp nhận collection ID từ client.</summary>
internal sealed class EvaluationFileOwnerResolver(IEvaluationFileSaveDbContext db, EvaluationAccessService access)
{
    public async Task<List<EvaluationFileOwner>> ResolveAsync(EvaluationFileResource resource, Guid parentId, bool write, CancellationToken ct)
    {
        var company = access.Company(write);
        if (write) await access.EmployeeAsync(company, ct);
        if (parentId == Guid.Empty) throw new EvaluationRequestException("ParentId không hợp lệ.");
        switch (resource)
        {
            case EvaluationFileResource.Iqc:
                access.Company(write, EvaluationTemplateType.Iqc);
                var qc = await db.QCInputByQCs.FirstOrDefaultAsync(x => x.QCInputByQCId == parentId && x.WarehouseVoucherDetail.Voucher.CompanyId == company, ct)
                    ?? throw Missing();
                var evaluation = await db.IqcInspectionEvaluations.Include(x => x.Results).ThenInclude(x => x.IqcSpecificationItem)
                    .ThenInclude(x => x!.IqcSpecification).FirstOrDefaultAsync(x => x.QCInputByQCId == qc.QCInputByQCId, ct);
                if (evaluation is null) return [];
                IqcEvaluationMapper.ValidateLegacy(evaluation, company);
                return evaluation.Results.Select(x =>
                {
                    var dto = IqcEvaluationMapper.ToResultDto(x);
                    return new EvaluationFileOwner(x.Id, dto.EvaluationStandardId, dto.Configuration,
                        x.AttachmentCollectionId, id => x.AttachmentCollectionId = id, true);
                }).ToList();
            case EvaluationFileResource.Coa:
                access.Company(write, EvaluationTemplateType.Coa);
                var coa = await db.CoaCertificates.Include(x => x.Results).ThenInclude(x => x.CoaSpecificationItem)
                    .ThenInclude(x => x!.CoaSpecification).FirstOrDefaultAsync(x => x.Id == parentId && x.CompanyId == company, ct)
                    ?? throw Missing();
                if (write && coa.Status != CoaCertificateStatus.Draft)
                    throw new EvaluationRequestException("Chỉ sửa file kết quả COA Draft.", 409);
                CoaCertificateService.ValidateLegacyCoa(coa, company);
                return coa.Results.Select(x =>
                {
                    var dto = CoaCertificateService.ResultDto(x);
                    return new EvaluationFileOwner(x.Id, dto.EvaluationStandardId, dto.Configuration,
                        x.AttachmentCollectionId, id => x.AttachmentCollectionId = id, true);
                }).ToList();
            default: throw new EvaluationRequestException("Loại bản ghi đính kèm không hợp lệ.");
        }
    }

    public async Task RequireExclusiveCollectionAsync(EvaluationFileOwner owner, CancellationToken ct)
    {
        if (owner.CollectionId is not { } id) return;
        // Không cho metadata/file từ collection chung hoặc chat vượt scope qua một liên kết sai.
        var links = await db.IqcInspectionResults.CountAsync(x => x.AttachmentCollectionId == id, ct)
            + await db.CoaCertificateResults.CountAsync(x => x.AttachmentCollectionId == id, ct);
        if (links != 1 || await db.QCInputByQCs.AnyAsync(x => x.AttachmentCollectionId == id, ct) ||
            await db.AttachmentModels.AnyAsync(x => x.AttachmentCollectionId == id && x.Slot != HRM.Domain.Enums.Attachment.AttachmentSlot.EvaluationAttachment, ct))
            throw new EvaluationRequestException("Collection không thuộc riêng chỉ tiêu này.", 403);
    }

    private static EvaluationRequestException Missing() => new("Không tìm thấy bản ghi đính kèm trong công ty hiện tại.", 404);
}
