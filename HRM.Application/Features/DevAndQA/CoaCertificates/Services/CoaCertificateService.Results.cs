using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Patching;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using HRM.Application.Features.DevAndQA.CoaCertificates.Dtos;
using HRM.Application.Commons.Authorization;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Devandqa;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.AddCoaCertificateResult;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificateResult;

namespace HRM.Application.Features.DevAndQA.CoaCertificates.Services;

/// <summary>Thêm và PATCH kết quả trên COA Draft; giữ snapshot của phiếu.</summary>
internal sealed partial class CoaCertificateService
{
    /// <summary>Chọn thêm chỉ tiêu từ danh mục vào COA Draft.</summary>
    public async Task<EvaluationResultDto> AddCoaResultAsync(AddCoaCertificateResultCommand request, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Coa);
        await access.EmployeeAsync(company, ct);
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var certificate = await LoadCoaAsync(request.CertificateId, company, ct);
        RequireDraft(certificate);
        if (certificate.Results.Count >= 200) throw new EvaluationRequestException("Phiếu vượt giới hạn 200 chỉ tiêu.");
        var standard = await access.StandardAsync(request.EvaluationStandardId, company, ct);
        if (certificate.Results.Any(x => x.EvaluationStandardId == standard.Id || x.CoaSpecificationItem?.EvaluationStandardId == standard.Id || string.Equals(x.ExternalId, standard.Code, StringComparison.OrdinalIgnoreCase)))
            throw new EvaluationRequestException("Chỉ tiêu đã có trên phiếu.", 409);
        var method = await access.MethodAsync(standard.MeasurementMethodId, company, ct);
        var row = EvaluationSnapshotFactory.CreateAdditionalCoa(standard, certificate.Id, company, method);
        row.SortOrder = checked(certificate.Results.Select(x => x.SortOrder).DefaultIfEmpty(0).Max() + 1);
        var configRequest = request.Configuration ?? new();
        var selected = await access.MethodAsync(configRequest.MeasurementMethodId, company, ct);
        var config = EvaluationConfiguration.Merge(EvaluationConfiguration.Read(row), configRequest, selected);
        EvaluationConfiguration.Apply(row, config);
        row.Value = EvaluationValueRules.Normalize(request.Value, config);
        db.CoaCertificateResults.Add(row);
        await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return ResultDto(row);
    }

    /// <summary>PATCH giá trị/cấu hình trên COA Draft.</summary>
    public async Task<EvaluationResultDto> PatchCoaResultAsync(PatchCoaCertificateResultCommand request, CancellationToken ct)
    {
        var company = access.Company(true, EvaluationTemplateType.Coa);
        await access.EmployeeAsync(company, ct);
        if (request.ClearValue && request.Value is not null) throw new EvaluationRequestException("Không vừa gửi value vừa clearValue.");
        using var guard = await mutationLock.AcquireAsync($"iqc:{company}", ct);
        await using var transaction = await db.BeginIqcTransactionAsync(ct);
        var certificate = await LoadCoaAsync(request.CertificateId, company, ct);
        RequireDraft(certificate);
        var row = certificate.Results.FirstOrDefault(x => x.Id == request.ResultId)
            ?? throw new EvaluationRequestException("Không tìm thấy dòng trên phiếu.", 404);
        var before = ResultDto(row);
        if (before.EvaluationStandardId is null) throw new EvaluationRequestException("Dữ liệu cũ thiếu nguồn chỉ tiêu; cần chuyển dữ liệu trước.");
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
        if (changed) await access.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return ResultDto(row);
    }

}
