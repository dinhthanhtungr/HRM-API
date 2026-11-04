using VietausWebAPI.Core.Domain.Enums.Devandqa;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Chỉ tiêu và cấu hình nhập được lưu riêng cho phiên bản tiêu chuẩn COA.</summary>
public class CoaSpecificationItem
{
    public Guid Id { get; set; }
    public Guid CoaSpecificationId { get; set; }
    public Guid EvaluationStandardId { get; set; }
    public string CriterionCode { get; set; } = string.Empty;
    public string CriterionName { get; set; } = string.Empty;
    public EvaluationDataType DataType { get; set; } = EvaluationDataType.Text;
    public string? InputConfigJson { get; set; }
    public string? Unit { get; set; }
    public Guid? MeasurementMethodId { get; set; }
    public string? MeasurementMethodCode { get; set; }
    public MeasurementMethod? MeasurementMethod { get; set; }
    public string? TestMethod { get; set; }
    public string? AcceptanceRuleJson { get; set; }
    public string? SpecificationText { get; set; }
    public bool IsRequired { get; set; }
    public bool ShowOnCoa { get; set; } = true;
    public int SortOrder { get; set; }
    public CoaSpecification CoaSpecification { get; set; } = null!;
    public EvaluationStandard EvaluationStandard { get; set; } = null!;
}
