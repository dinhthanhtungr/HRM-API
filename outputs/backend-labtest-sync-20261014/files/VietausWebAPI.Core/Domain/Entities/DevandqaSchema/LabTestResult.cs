using VietausWebAPI.Core.Domain.Entities.AttachmentSchema;
using VietausWebAPI.Core.Domain.Enums.Devandqa;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Kết quả một chỉ tiêu của phiếu test; cấu hình là snapshot, không tự đổi theo danh mục.</summary>
public class LabTestResult
{
    public Guid Id { get; set; }
    public Guid LabTestId { get; set; }
    public Guid? EvaluationStandardId { get; set; }
    /// <summary>ID nguồn tham khảo, không có FK để template có thể thay đổi hoặc bỏ dòng.</summary>
    public Guid? SourceTemplateItemId { get; set; }
    public string CriterionCode { get; set; } = string.Empty;
    public string CriterionName { get; set; } = string.Empty;
    public string? CriterionNameEn { get; set; }
    public EvaluationDataType DataType { get; set; } = EvaluationDataType.Text;
    public string? Unit { get; set; }
    public Guid? MeasurementMethodId { get; set; }
    public string? MeasurementMethodCode { get; set; }
    public string? TestMethod { get; set; }
    public string? InputConfigJson { get; set; }
    public string? AcceptanceRuleJson { get; set; }
    public string? SpecificationText { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Giá trị đo dạng scalar; kiểu Attachment dùng nhóm tệp, không dùng Value.</summary>
    public string? Value { get; set; }
    public Guid? AttachmentCollectionId { get; set; }
    public LabTest LabTest { get; set; } = null!;
    public EvaluationStandard? EvaluationStandard { get; set; }
    public MeasurementMethod? MeasurementMethod { get; set; }
    public AttachmentCollection? AttachmentCollection { get; set; }
}
