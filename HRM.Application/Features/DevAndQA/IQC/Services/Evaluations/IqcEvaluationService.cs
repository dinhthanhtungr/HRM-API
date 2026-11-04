using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Patching;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Features.DevAndQA.IQC.Commands.InitializeIqcEvaluation;
using HRM.Application.Features.DevAndQA.IQC.Commands.AddIqcEvaluationResult;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchIqcEvaluationResult;

namespace HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;

/// <summary>Đánh giá IQC: đọc snapshot/preview, khởi tạo, chọn thêm và sửa kết quả.</summary>
internal sealed partial class IqcEvaluationService(
    IEvaluationDbContext db,
    EvaluationAccessService access,
    KeyedMutationLock<string> mutationLock,
    EvaluationTemplateService templates)
{
    private async Task<QCInputByQC> LoadQcAsync(long voucherDetailId, Guid company, CancellationToken ct)
    {
        if (voucherDetailId <= 0)
        {
            throw new EvaluationRequestException("VoucherDetailId không hợp lệ.");
        }

        return await db.QCInputByQCs
            .Where(x => x.VoucherDetailId == voucherDetailId && x.WarehouseVoucherDetail.Voucher.CompanyId == company)
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.QCInputByQCId)
            .FirstOrDefaultAsync(ct)
            ?? throw new EvaluationRequestException("Chưa có phiếu QC hoặc phiếu ngoài công ty. Lưu phiếu QC trước.", 404);
    }

    private async Task<IqcInspectionEvaluation> LoadIqcEvaluationAsync(long voucherDetailId, Guid company, CancellationToken ct)
    {
        var qc = await LoadQcAsync(voucherDetailId, company, ct);
        var evaluation = await db.IqcInspectionEvaluations
            .Include(x => x.Results)
            .ThenInclude(x => x.IqcSpecificationItem)
            .ThenInclude(x => x!.IqcSpecification)
            .FirstOrDefaultAsync(x => x.QCInputByQCId == qc.QCInputByQCId, ct)
            ?? throw new EvaluationRequestException("Phiếu QC chưa khởi tạo chỉ tiêu.", 404);

        ValidateLegacyIqc(evaluation, company);

        return evaluation;
    }

    private static void ValidateLegacyIqc(IqcInspectionEvaluation evaluation, Guid company) =>
        IqcEvaluationMapper.ValidateLegacy(evaluation, company);

    /// <summary>Đọc snapshot đã lưu hoặc preview template mặc định; không ghi DB khi mở màn hình.</summary>
    public async Task<IqcEvaluationDto> GetIqcEvaluationAsync(long voucherDetailId, CancellationToken ct)
    {
        var company = access.Company(false, EvaluationTemplateType.Iqc);
        if (voucherDetailId <= 0)
        {
            throw new EvaluationRequestException("VoucherDetailId không hợp lệ.");
        }

        if (!await db.WarehouseVoucherDetails
            .AsNoTracking()
            .AnyAsync(x => x.VoucherDetailId == voucherDetailId && x.Voucher.CompanyId == company, ct))
        {
            throw new EvaluationRequestException("Không tìm thấy dòng phiếu trong công ty hiện tại.", 404);
        }

        var qc = await db.QCInputByQCs
            .AsNoTracking()
            .Where(x => x.VoucherDetailId == voucherDetailId && x.WarehouseVoucherDetail.Voucher.CompanyId == company)
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.QCInputByQCId)
            .FirstOrDefaultAsync(ct);
        if (qc is not null)
        {
            var stored = await db.IqcInspectionEvaluations
                .AsNoTracking()
                .Include(x => x.Results)
                .ThenInclude(x => x.IqcSpecificationItem)
                .ThenInclude(x => x!.IqcSpecification)
                .FirstOrDefaultAsync(x => x.QCInputByQCId == qc.QCInputByQCId, ct);
            if (stored is not null)
            {
                ValidateLegacyIqc(stored, company);
                return IqcDto(stored);
            }
        }

        var now = DateTime.Now;
        if (!await db.EvaluationTemplates.AnyAsync(x =>
            x.CompanyId == company &&
            x.Type == EvaluationTemplateType.Iqc &&
            x.IsDefault &&
            x.Status == EvaluationTemplateStatus.Published &&
            (x.EffectiveFrom == null || x.EffectiveFrom <= now), ct))
        {
            return new(null, qc?.QCInputByQCId, null, null, null, null, [], false);
        }

        var template = await templates.SelectTemplateAsync(null, EvaluationTemplateType.Iqc, company, ct);
        var preview = EvaluationSnapshotFactory.CreateIqc(template, qc?.QCInputByQCId ?? Guid.NewGuid(), company);
        var rows = preview.Results
            .Select(x => ResultDto(x) with { Id = Guid.Empty, HasConfigurationSnapshot = false })
            .ToList();

        return new(null, qc?.QCInputByQCId, template.Id, template.Code, template.Name, template.Version, rows, false);
    }

    /// <summary>Khởi tạo chủ động trên QC đã lưu; gọi lặp không reset kết quả.</summary>
    public async Task<IqcEvaluationDto> InitializeIqcAsync(InitializeIqcEvaluationCommand request, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Iqc);
        await access.EmployeeAsync(company, ct);

        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);

        var qc = await LoadQcAsync(request.VoucherDetailId, company, ct);

        var existing = await db.IqcInspectionEvaluations
            .Include(x => x.Results)
            .ThenInclude(x => x.IqcSpecificationItem)
            .ThenInclude(x => x!.IqcSpecification)
            .FirstOrDefaultAsync(x => x.QCInputByQCId == qc.QCInputByQCId, ct);
        if (existing is not null)
        {
            ValidateLegacyIqc(existing, company);
            return IqcDto(existing);
        }

        var template = await templates.SelectTemplateAsync(request.EvaluationTemplateId, EvaluationTemplateType.Iqc, company, ct);
        var evaluation = EvaluationSnapshotFactory.CreateIqc(template, qc.QCInputByQCId, company);
        db.IqcInspectionEvaluations.Add(evaluation);

        await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);

        return IqcDto(evaluation);
    }

    /// <summary>Chọn thêm chỉ tiêu vào QC đã lưu, tự tạo đầu đánh giá nếu cần.</summary>
    public async Task<EvaluationResultDto> AddIqcResultAsync(AddIqcEvaluationResultCommand request, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Iqc);
        await access.EmployeeAsync(company, ct);

        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);

        var qc = await LoadQcAsync(request.VoucherDetailId, company, ct);

        var existing = await db.IqcInspectionEvaluations
            .Include(x => x.Results)
            .ThenInclude(x => x.IqcSpecificationItem)
            .ThenInclude(x => x!.IqcSpecification)
            .FirstOrDefaultAsync(x => x.QCInputByQCId == qc.QCInputByQCId, ct);
        if (existing is not null)
        {
            ValidateLegacyIqc(existing, company);
        }

        var standard = await access.StandardAsync(request.EvaluationStandardId, company, ct);
        if (existing?.Results.Any(x =>
            x.EvaluationStandardId == standard.Id ||
            x.IqcSpecificationItem?.EvaluationStandardId == standard.Id ||
            string.Equals(x.ExternalId, standard.Code, StringComparison.OrdinalIgnoreCase)) == true)
        {
            throw new EvaluationRequestException("Chỉ tiêu đã có trên phiếu.", 409);
        }

        var evaluation = await StageIqcSaveAsync(qc,
            new(Results: [new(standard.Id, Value: request.Value, Configuration: request.Configuration)]), company, ct,
            persistEmptySelection: true);
        var row = evaluation!.Results.Single(x => x.EvaluationStandardId == standard.Id);

        await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);

        return ResultDto(row);
    }

    /// <summary>Sửa giá trị/cấu hình của đúng dòng trên phiếu QC.</summary>
    public async Task<EvaluationResultDto> PatchIqcResultAsync(PatchIqcEvaluationResultCommand request, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Iqc);
        await access.EmployeeAsync(company, ct);
        if (request.ClearValue && request.Value is not null)
        {
            throw new EvaluationRequestException("Không vừa gửi value vừa clearValue.");
        }

        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);

        var evaluation = await LoadIqcEvaluationAsync(request.VoucherDetailId, company, ct);
        var row = evaluation.Results.FirstOrDefault(x => x.Id == request.ResultId)
            ?? throw new EvaluationRequestException("Không tìm thấy dòng trên phiếu.", 404);

        var before = ResultDto(row);
        if (before.EvaluationStandardId is null)
        {
            throw new EvaluationRequestException("Dữ liệu cũ thiếu nguồn chỉ tiêu; cần chuyển dữ liệu trước.");
        }

        var configRequest = request.Configuration ?? new();
        var selected = await access.MethodAsync(configRequest.MeasurementMethodId, company, ct);
        var config = EvaluationConfiguration.Merge(before.Configuration, configRequest, selected);
        var value = EvaluationValueRules.Normalize(request.ClearValue ? null : request.Value ?? row.Value, config);
        var changed = EvaluationConfiguration.Apply(row, config);
        changed |= PatchHelper.Set(value, () => row.Value, x => row.Value = x);
        if (!row.HasConfigurationSnapshot)
        {
            row.EvaluationStandardId = before.EvaluationStandardId;
            row.CriterionCode = before.CriterionCode;
            row.CriterionName = before.CriterionName;
            row.CriterionNameEn = before.CriterionNameEn;
            row.HasConfigurationSnapshot = true;
            changed = true;
        }
        if (changed)
        {
            await access.SaveAsync(ct);
        }

        await transaction.CommitAsync(ct);

        return ResultDto(row);
    }

    private static EvaluationResultDto ResultDto(IqcInspectionResult row) => IqcEvaluationMapper.ToResultDto(row);

    private static IqcEvaluationDto IqcDto(IqcInspectionEvaluation evaluation) => IqcEvaluationMapper.ToDto(evaluation);
}
