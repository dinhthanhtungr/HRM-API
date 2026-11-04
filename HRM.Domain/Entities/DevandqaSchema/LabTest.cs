using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.DevandqaSchema;

/// <summary>Phiếu kiểm nghiệm mẫu độc lập; không liên kết voucher, tồn kho hoặc QC đầu vào.</summary>
public class LabTest
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    /// <summary>Mã phiếu do backend sinh, ví dụ NVL_TEST_00001; duy nhất trong công ty.</summary>
    public string Code { get; set; } = string.Empty;
    public string SampleName { get; set; } = string.Empty;
    /* Tạm dừng mở rộng LabTest; giữ source để bổ sung lại sau.
    public string? SupplierCustomerName { get; set; }
    public string? BatchNumber { get; set; }
    /// <summary>Mã hàng gốc từ Excel; không phải mã phiếu Code.</summary>
    public string? ItemCode { get; set; }
    public string? Resin { get; set; }
    public string? Application { get; set; }
    /// <summary>Người test trong dữ liệu nguồn; khác nhân viên audit thao tác import.</summary>
    public string? PreparedByName { get; set; }
    public decimal? WeightKg { get; set; }
    /// <summary>Chỉ tên file tham khảo từ Excel; không dùng làm đường dẫn đọc file.</summary>
    public string? ImportedPhotoName { get; set; }
    public bool IsDeleted { get; set; }
    /// <summary>Khóa chống import lặp theo company/batch/dòng, kể cả phiếu đã xóa mềm.</summary>
    public Guid? ImportBatchId { get; set; }
    public int? ImportRowNumber { get; set; }
    */
    public DateOnly TestDate { get; set; }
    public string? Note { get; set; }
    public Guid? EvaluationTemplateId { get; set; }
    public string? TemplateCode { get; set; }
    public string? TemplateName { get; set; }
    public int? TemplateVersion { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Company Company { get; set; } = null!;
    public EvaluationTemplate? EvaluationTemplate { get; set; }
    public Employee CreatedByEmployee { get; set; } = null!;
    public Employee? UpdatedByEmployee { get; set; }
    public ICollection<LabTestResult> Results { get; set; } = new List<LabTestResult>();
}
