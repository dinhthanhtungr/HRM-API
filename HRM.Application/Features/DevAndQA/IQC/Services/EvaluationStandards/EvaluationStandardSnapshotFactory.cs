using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Application.Features.DevAndQA.IQC.Services.EvaluationStandards;

/// <summary>Tạo Item với bản sao cấu hình mặc định, không chia sẻ object hoặc cập nhật Item cũ khi sửa danh mục.</summary>
internal static class EvaluationStandardSnapshotFactory
{
    public static IqcSpecificationItem CreateIqcItem(EvaluationStandard standard, Guid specificationId) => new()
    {
        Id = Guid.CreateVersion7(),
        IqcSpecificationId = specificationId,
        EvaluationStandardId = standard.Id,
        CriterionCode = standard.Code,
        CriterionName = standard.Name,
        CriterionNameEn = standard.NameEn,
        DataType = standard.DataType,
        Unit = standard.Unit,
        MeasurementMethodId = standard.MeasurementMethodId,
        MeasurementMethodCode = standard.MeasurementMethod?.Code,
        TestMethod = standard.MeasurementMethod?.Name ?? standard.TestMethod,
        InputConfigJson = standard.InputConfigJson,
        AcceptanceRuleJson = standard.AcceptanceRuleJson,
        SpecificationText = standard.SpecificationText,
        IsRequired = standard.IsRequired,
        SortOrder = standard.SortOrder
    };

    public static CoaSpecificationItem CreateCoaItem(EvaluationStandard standard, Guid specificationId) => new()
    {
        Id = Guid.CreateVersion7(),
        CoaSpecificationId = specificationId,
        EvaluationStandardId = standard.Id,
        CriterionCode = standard.Code,
        CriterionName = standard.Name,
        CriterionNameEn = standard.NameEn,
        DataType = standard.DataType,
        Unit = standard.Unit,
        MeasurementMethodId = standard.MeasurementMethodId,
        MeasurementMethodCode = standard.MeasurementMethod?.Code,
        TestMethod = standard.MeasurementMethod?.Name ?? standard.TestMethod,
        InputConfigJson = standard.InputConfigJson,
        AcceptanceRuleJson = standard.AcceptanceRuleJson,
        SpecificationText = standard.SpecificationText,
        IsRequired = standard.IsRequired,
        SortOrder = standard.SortOrder
    };
}
