using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Dtos;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Application.Features.DevAndQA.LabTests.Services.Basic;

internal static class BasicLabTestMapper
{
    public static EvaluationConfigurationDto Configuration(LabTestResult x) => new(x.DataType, x.Unit,
        x.MeasurementMethodId, x.MeasurementMethodCode, x.TestMethod, x.InputConfigJson, x.AcceptanceRuleJson,
        x.SpecificationText, x.IsRequired, x.SortOrder);

    public static BasicLabTestDto ToDto(LabTest x) => new(x.Id, x.Code, x.SampleName, x.TestDate, x.Note,
        x.EvaluationTemplateId, x.TemplateCode, x.TemplateName, x.TemplateVersion, x.CreatedDate, x.CreatedBy,
        x.Results.OrderBy(r => r.SortOrder).ThenBy(r => r.Id).Select(r => new EvaluationResultDto(r.Id,
            r.EvaluationStandardId, r.SourceTemplateItemId, r.CriterionCode, r.CriterionCode, r.CriterionName,
            true, r.Value, Configuration(r), r.CriterionNameEn, r.AttachmentCollectionId)).ToList(), x.BatchNumber, x.WeightKg,
        x.SupplierId, x.Supplier?.CompanyId == x.CompanyId ? x.Supplier.SupplierName : null,
        x.CreatedByEmployee?.CompanyId == x.CompanyId ? x.CreatedByEmployee.FullName : null,
        x.MaterialId, x.MaterialCode);

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
