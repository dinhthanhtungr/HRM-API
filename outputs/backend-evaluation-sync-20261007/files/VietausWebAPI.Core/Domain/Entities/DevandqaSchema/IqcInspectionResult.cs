using VietausWebAPI.Core.Domain.Enums.Devandqa;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Giá trị của một chỉ tiêu trong phiếu IQC; lưu bản sao cấu hình/yêu cầu để bảo toàn lịch sử.</summary>
public class IqcInspectionResult
{
    public Guid Id { get; set; }
    public Guid IqcInspectionEvaluationId { get; set; }
    public Guid? IqcSpecificationItemId { get; set; }
    /// <summary>Key đọc được của chỉ tiêu, ví dụ MOISTURE; lấy từ CriterionCode của standard item.</summary>
    public string ExternalId { get; set; } = string.Empty;
    public Guid? SourceTemplateItemId { get; set; }
    public Guid? EvaluationStandardId { get; set; }
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
    /// <summary>False chỉ dành cho dữ liệu cũ chưa backfill snapshot; phiếu mới phải đặt True.</summary>
    public bool HasConfigurationSnapshot { get; set; }
    public EvaluationTemplateItem? SourceTemplateItem { get; set; }
    public EvaluationStandard? EvaluationStandard { get; set; }
    public MeasurementMethod? MeasurementMethod { get; set; }
    public string? Value { get; set; }
    public IqcInspectionEvaluation IqcInspectionEvaluation { get; set; } = null!;
    public IqcSpecificationItem? IqcSpecificationItem { get; set; }
}
