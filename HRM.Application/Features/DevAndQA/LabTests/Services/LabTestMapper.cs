using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Application.Features.DevAndQA.LabTests.Services;

internal static class LabTestMapper
{
    public static EvaluationConfigurationDto Configuration(LabTestResult x) => new(x.DataType, x.Unit,
        x.MeasurementMethodId, x.MeasurementMethodCode, x.TestMethod, x.InputConfigJson,
        x.AcceptanceRuleJson, x.SpecificationText, x.IsRequired, x.SortOrder);

    public static LabTestDto ToDto(LabTest x) => new(x.Id, x.Code, x.TestDate, x.SampleName,
        x.SupplierCustomerName, x.BatchNumber, x.ItemCode, x.Resin, x.Application, x.PreparedByName,
        x.Note, x.WeightKg, x.ImportedPhotoName, x.EvaluationTemplateId, x.TemplateCode, x.TemplateName,
        x.TemplateVersion, x.CreatedDate, x.CreatedBy, x.UpdatedDate, x.UpdatedBy,
        x.Results.OrderBy(r => r.SortOrder).ThenBy(r => r.Id).Select(r => new LabTestResultDto(r.Id,
            r.EvaluationStandardId, r.CriterionCode, r.CriterionName, r.CriterionNameEn,
            Configuration(r), r.Value, null, r.AttachmentCollectionId)).ToList());

    public static LabTestResult Snapshot(EvaluationTemplateItem x, Guid parent, bool fromTemplate) => new()
    {
        Id = Guid.CreateVersion7(), LabTestId = parent, EvaluationStandardId = x.EvaluationStandardId,
        SourceTemplateItemId = fromTemplate ? x.Id : null, CriterionCode = x.CriterionCode,
        CriterionName = x.CriterionName, CriterionNameEn = x.CriterionNameEn, DataType = x.DataType,
        Unit = x.Unit, MeasurementMethodId = x.MeasurementMethodId, MeasurementMethodCode = x.MeasurementMethodCode,
        TestMethod = x.TestMethod, InputConfigJson = x.InputConfigJson, AcceptanceRuleJson = x.AcceptanceRuleJson,
        SpecificationText = x.SpecificationText, IsRequired = x.IsRequired, SortOrder = x.SortOrder
    };
}
