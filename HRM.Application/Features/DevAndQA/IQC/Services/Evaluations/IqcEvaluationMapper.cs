using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;

/// <summary>Ánh xạ snapshot IQC và kiểm tra nguồn legacy; dùng cho chi tiết, ghi và lịch sử IQC.</summary>
internal static class IqcEvaluationMapper
{
    public static void ValidateLegacy(IqcInspectionEvaluation evaluation, Guid company)
    {
        if (evaluation.Results.Where(x => !x.HasConfigurationSnapshot).Any(x =>
            x.IqcSpecificationItem is not { } item || item.IqcSpecificationId != evaluation.IqcSpecificationId ||
            item.IqcSpecification is null || item.IqcSpecification.CompanyId != company))
            throw new EvaluationRequestException("Nguồn tiêu chuẩn cũ không hợp lệ; cần kiểm tra dữ liệu trước.");
    }


    public static EvaluationResultDto ToResultDto(IqcInspectionResult x)
    {
        var config = EvaluationConfiguration.Read(x);
        var code = x.CriterionCode;
        var name = x.CriterionName;
        var nameEn = x.CriterionNameEn;
        var standardId = x.EvaluationStandardId;
        if (!x.HasConfigurationSnapshot && x.IqcSpecificationItem is { } old)
        {
            config = new(old.DataType, old.Unit, old.MeasurementMethodId, old.MeasurementMethodCode, old.TestMethod,
                old.InputConfigJson, old.AcceptanceRuleJson, old.SpecificationText, old.IsRequired,
                old.SortOrder);
            code = old.CriterionCode; name = old.CriterionName;
            nameEn = old.CriterionNameEn; standardId = old.EvaluationStandardId;
        }
        return new(x.Id, standardId, x.SourceTemplateItemId, x.ExternalId, code, name, x.HasConfigurationSnapshot, x.Value, config, nameEn, x.AttachmentCollectionId);
    }

    public static IqcEvaluationDto ToDto(IqcInspectionEvaluation x) => new(x.Id, x.QCInputByQCId,
        x.EvaluationTemplateId, x.TemplateCode, x.TemplateName, x.TemplateVersion,
        x.Results.OrderBy(i => i.SortOrder).Select(ToResultDto).ToList());
}
