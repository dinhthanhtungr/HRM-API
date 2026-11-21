using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Domain.Entities.DevandqaSchema;

/// <summary>Phiếu kiểm nghiệm mẫu độc lập; không liên kết voucher, tồn kho hoặc QC đầu vào.</summary>
public class LabTest
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    /// <summary>Mã phiếu do backend sinh, ví dụ NVL_TEST_00001; duy nhất trong công ty.</summary>
    public string Code { get; set; } = string.Empty;
    public string SampleName { get; set; } = string.Empty;
    /// <summary>NVL có sẵn được chọn; null là mẫu nhập tự do, không cần phiếu kho.</summary>
    public Guid? MaterialId { get; set; }
    /// <summary>Mã NVL tại lần chọn; SampleName lưu tên cùng thời điểm, không tự đổi theo danh mục.</summary>
    public string? MaterialCode { get; set; }
    /// <summary>Nhà cung cấp của mẫu; có thể chưa xác định.</summary>
    public Guid? SupplierId { get; set; }
    /// <summary>Số lô do người test nhập; không liên kết lô nhập kho.</summary>
    public string? BatchNumber { get; set; }
    /// <summary>Khối lượng mẫu do người test nhập, đơn vị kg; null là chưa nhập.</summary>
    public decimal? WeightKg { get; set; }
    /* Tạm dừng mở rộng LabTest; giữ source để bổ sung lại sau.
    public string? SupplierCustomerName { get; set; }
    /// <summary>Mã hàng gốc từ Excel; không phải mã phiếu Code.</summary>
    public string? ItemCode { get; set; }
    public string? Resin { get; set; }
    public string? Application { get; set; }
    /// <summary>Người test trong dữ liệu nguồn; khác nhân viên audit thao tác import.</summary>
    public string? PreparedByName { get; set; }
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
    public Supplier? Supplier { get; set; }
    public Material? Material { get; set; }
    public EvaluationTemplate? EvaluationTemplate { get; set; }
    public Employee CreatedByEmployee { get; set; } = null!;
    public Employee? UpdatedByEmployee { get; set; }
    public ICollection<LabTestResult> Results { get; set; } = new List<LabTestResult>();
}
