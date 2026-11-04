using HRM.Domain.Enums.Devandqa;

namespace HRM.Domain.Entities.DevandqaSchema;


/// <summary>Chỉ tiêu và cấu hình nhập được lưu riêng cho phiên bản tiêu chuẩn IQC.</summary>
public class IqcSpecificationItem
{
    public Guid Id { get; set; }
    /// <summary>Nhóm tài liệu tham khảo của dòng tiêu chuẩn; snapshot phải tạo collection riêng.</summary>
    public Guid IqcSpecificationId { get; set; }
    public Guid EvaluationStandardId { get; set; }
    public string CriterionCode { get; set; } = string.Empty;
    public string CriterionName { get; set; } = string.Empty;
    /// <summary>Tên tiếng Anh lưu tại thời điểm tạo; không tự đổi theo danh mục.</summary>
    public string? CriterionNameEn { get; set; }
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
    public int SortOrder { get; set; }
    public IqcSpecification IqcSpecification { get; set; } = null!;
    public EvaluationStandard EvaluationStandard { get; set; } = null!;
}
