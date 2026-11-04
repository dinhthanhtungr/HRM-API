using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Entities.AttachmentSchema;

namespace HRM.Domain.Entities.DevandqaSchema;

/// <summary>Giá trị của một chỉ tiêu trong phiếu IQC; lưu bản sao cấu hình/yêu cầu để bảo toàn lịch sử.</summary>
public class IqcInspectionResult
{
    public Guid Id { get; set; }
    /// <summary>Nhóm file minh chứng riêng của dòng kết quả IQC; không sao chép ID collection từ mẫu.</summary>
    public Guid? AttachmentCollectionId { get; set; }
    public AttachmentCollection? AttachmentCollection { get; set; }
    public Guid IqcInspectionEvaluationId { get; set; }
    public Guid? IqcSpecificationItemId { get; set; }
    /// <summary>Key đọc được của chỉ tiêu, ví dụ MOISTURE; lấy từ CriterionCode của standard item.</summary>
    public string ExternalId { get; set; } = string.Empty;
    /// <summary>ID dòng template nguồn để tham khảo; không có FK, nguồn có thể đã bị xóa. Đọc cấu hình từ snapshot.</summary>
    public Guid? SourceTemplateItemId { get; set; }
    public Guid? EvaluationStandardId { get; set; }
    public string CriterionCode { get; set; } = string.Empty;
    public string CriterionName { get; set; } = string.Empty;
    /// <summary>Tên tiếng Anh lưu tại thời điểm tạo; không tự đổi theo danh mục.</summary>
    public string? CriterionNameEn { get; set; }
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
    public int SortOrder { get; set; }
    /// <summary>False chỉ dành cho dữ liệu cũ chưa backfill snapshot; phiếu mới phải đặt True.</summary>
    public bool HasConfigurationSnapshot { get; set; }
    public EvaluationStandard? EvaluationStandard { get; set; }
    public MeasurementMethod? MeasurementMethod { get; set; }
    public string? Value { get; set; }
    public IqcInspectionEvaluation IqcInspectionEvaluation { get; set; } = null!;
    public IqcSpecificationItem? IqcSpecificationItem { get; set; }
}
