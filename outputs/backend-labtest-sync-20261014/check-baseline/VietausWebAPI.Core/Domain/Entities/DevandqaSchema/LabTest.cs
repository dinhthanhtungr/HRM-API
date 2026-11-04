using VietausWebAPI.Core.Domain.Entities.CompanySchema;
using VietausWebAPI.Core.Domain.Entities.HrSchema;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Phiếu kiểm nghiệm mẫu độc lập; không liên kết voucher, tồn kho hoặc QC đầu vào.</summary>
public class LabTest
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    /// <summary>Mã phiếu do backend sinh, ví dụ NVL_TEST_00001; duy nhất trong công ty.</summary>
    public string Code { get; set; } = string.Empty;
    public string SampleName { get; set; } = string.Empty;
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
