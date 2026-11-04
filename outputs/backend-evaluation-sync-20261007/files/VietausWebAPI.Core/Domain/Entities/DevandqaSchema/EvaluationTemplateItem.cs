using VietausWebAPI.Core.Domain.Enums.Devandqa;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Dòng template lưu cấu hình riêng, không tự đổi theo danh mục.</summary>
public class EvaluationTemplateItem
{
    public Guid Id { get; set; }
    public Guid EvaluationTemplateId { get; set; }
    public Guid EvaluationStandardId { get; set; }
    public string CriterionCode { get; set; } = string.Empty;
    public string CriterionName { get; set; } = string.Empty;
    public EvaluationDataType DataType { get; set; } = EvaluationDataType.Text;
    public string? Unit { get; set; }
    public Guid? MeasurementMethodId { get; set; }
    /// <summary>Bản sao mã phương pháp đo tại thời điểm tạo dòng.</summary>
    public string? MeasurementMethodCode { get; set; }
    public string? TestMethod { get; set; }
    public string? InputConfigJson { get; set; }
    public string? AcceptanceRuleJson { get; set; }
    public string? SpecificationText { get; set; }
    public bool IsRequired { get; set; }
    public bool ShowOnReport { get; set; } = true;
    public bool ShowOnCoa { get; set; } = true;
    public int SortOrder { get; set; }
    public EvaluationTemplate EvaluationTemplate { get; set; } = null!;
    public EvaluationStandard EvaluationStandard { get; set; } = null!;
    public MeasurementMethod? MeasurementMethod { get; set; }
}
