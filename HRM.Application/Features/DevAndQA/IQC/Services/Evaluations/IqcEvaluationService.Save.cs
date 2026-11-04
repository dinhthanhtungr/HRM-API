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

namespace HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;

/// <summary>Lưu đánh giá bổ sung trong transaction của phiếu QC; không tự Save/Commit.</summary>
internal sealed partial class IqcEvaluationService
{
    /// <summary>Chuẩn bị kết quả bổ sung trong transaction của IqcWriter; không lock/Save/Commit riêng.</summary>
    public async Task<IqcInspectionEvaluation?> StageIqcSaveAsync(
        QCInputByQC qc,
        IqcEvaluationSaveRequest? request,
        Guid company,
        CancellationToken ct,
        bool persistEmptySelection = false)
    {
        if (request?.Results is not { Count: > 0 } requests)
        {
            return null;
        }

        if (access.Company(true, EvaluationTemplateType.Iqc) != company)
        {
            throw new EvaluationRequestException("Company không hợp lệ.", 403);
        }

        if (requests.Count > 200 || requests.Any(x => x is null) ||
            requests.Select(x => x.EvaluationStandardId).Distinct().Count() != requests.Count)
        {
            throw new EvaluationRequestException("Danh sách kết quả không hợp lệ, trùng hoặc vượt 200 dòng.");
        }

        var evaluation = await db.IqcInspectionEvaluations
            .Include(x => x.Results)
            .ThenInclude(x => x.IqcSpecificationItem)
            .ThenInclude(x => x!.IqcSpecification)
            .FirstOrDefaultAsync(x => x.QCInputByQCId == qc.QCInputByQCId, ct);

        var isNew = evaluation is null;

        if (evaluation is not null)
        {
            ValidateLegacyIqc(evaluation, company);
            if (request.EvaluationTemplateId.HasValue && request.EvaluationTemplateId != evaluation.EvaluationTemplateId)
            {
                throw new EvaluationRequestException("Không đổi template của phiếu đã lưu.", 409);
            }
        }
        else
        {
            evaluation = request.EvaluationTemplateId.HasValue
                ? EvaluationSnapshotFactory.CreateIqc(await templates.SelectTemplateAsync(request.EvaluationTemplateId,
                    EvaluationTemplateType.Iqc, company, ct), qc.QCInputByQCId, company)
                : new IqcInspectionEvaluation
                {
                    Id = Guid.CreateVersion7(),
                    QCInputByQCId = qc.QCInputByQCId
                };
            evaluation.QCInputByQC = qc;
        }

        var nextSortOrder = evaluation.Results
            .Select(x => x.SortOrder)
            .DefaultIfEmpty(0)
            .Max();
        var plans = new List<(
            IqcInspectionResult Row,
            EvaluationResultDto Before,
            EvaluationConfigurationDto Config,
            string? Value,
            bool Add)>();

        foreach (var input in requests)
        {
            if (input.ClearValue && input.Value is not null)
            {
                throw new EvaluationRequestException("Không vừa gửi value vừa clearValue.");
            }

            IqcInspectionResult? row;
            if (input.ResultId.HasValue)
            {
                row = evaluation.Results.FirstOrDefault(x => x.Id == input.ResultId);
                if (row is null || isNew)
                {
                    throw new EvaluationRequestException("Không tìm thấy dòng trên phiếu.", 404);
                }
            }
            else
            {
                row = evaluation.Results.FirstOrDefault(x =>
                    x.EvaluationStandardId == input.EvaluationStandardId ||
                    x.IqcSpecificationItem?.EvaluationStandardId == input.EvaluationStandardId);
            }

            var add = row is null;
            if (add)
            {
                if (input.SourceTemplateItemId.HasValue)
                {
                    throw new EvaluationRequestException("Dòng không thuộc template của phiếu.");
                }
                var standard = await access.StandardAsync(input.EvaluationStandardId, company, ct);
                if (evaluation.Results.Any(x => string.Equals(x.ExternalId, standard.Code, StringComparison.OrdinalIgnoreCase)) ||
                    plans.Any(x => string.Equals(x.Row.ExternalId, standard.Code, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new EvaluationRequestException("Chỉ tiêu đã có trên phiếu.", 409);
                }
                row = EvaluationSnapshotFactory.CreateAdditionalIqc(standard, evaluation.Id, company,
                    await access.MethodAsync(standard.MeasurementMethodId, company, ct));
                row.SortOrder = checked(++nextSortOrder);
            }

            var before = ResultDto(row!);
            if (before.EvaluationStandardId != input.EvaluationStandardId ||
                input.SourceTemplateItemId.HasValue && input.SourceTemplateItemId != row!.SourceTemplateItemId)
            {
                throw new EvaluationRequestException("Nguồn chỉ tiêu không khớp dòng của phiếu.");
            }

            var configRequest = input.Configuration ?? new();
            var config = EvaluationConfiguration.Merge(before.Configuration, configRequest,
                await access.MethodAsync(configRequest.MeasurementMethodId, company, ct));
            var value = EvaluationValueRules.Normalize(input.ClearValue ? null : input.Value ?? row!.Value, config);
            plans.Add((row!, before, config, value, add));
        }

        if (evaluation.Results.Count + plans.Count(x => x.Add) > 200)
        {
            throw new EvaluationRequestException("Phiếu vượt giới hạn 200 chỉ tiêu.");
        }

        // FE có thể gửi các dòng preview chưa nhập; không tạo đánh giá chỉ vì mở/lưu form QC.
        if (isNew && !persistEmptySelection && plans.All(x => x.Value is null && x.Config.DataType != HRM.Domain.Enums.Devandqa.EvaluationDataType.Attachment))
        {
            return null;
        }

        // Chỉ mutate sau khi toàn bộ payload đã hợp lệ; snapshot cũ giữ nguyên nếu không được gửi.
        foreach (var plan in plans)
        {
            EvaluationConfiguration.Apply(plan.Row, plan.Config);
            PatchHelper.Set(plan.Value, () => plan.Row.Value, x => plan.Row.Value = x);
            if (!plan.Row.HasConfigurationSnapshot)
            {
                plan.Row.EvaluationStandardId = plan.Before.EvaluationStandardId;
                plan.Row.CriterionCode = plan.Before.CriterionCode;
                plan.Row.CriterionName = plan.Before.CriterionName;
                plan.Row.CriterionNameEn = plan.Before.CriterionNameEn;
                plan.Row.HasConfigurationSnapshot = true;
            }

            if (plan.Add)
            {
                evaluation.Results.Add(plan.Row);
                if (!isNew)
                {
                    db.IqcInspectionResults.Add(plan.Row);
                }
            }
        }

        if (isNew)
        {
            db.IqcInspectionEvaluations.Add(evaluation);
        }

        return evaluation;
    }
}
