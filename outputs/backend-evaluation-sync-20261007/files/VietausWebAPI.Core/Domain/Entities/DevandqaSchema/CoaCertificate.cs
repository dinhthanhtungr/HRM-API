using VietausWebAPI.Core.Domain.Entities.CompanySchema;
using VietausWebAPI.Core.Domain.Entities.HrSchema;
using VietausWebAPI.Core.Domain.Enums.Devandqa;

namespace VietausWebAPI.Core.Domain.Entities.DevandqaSchema;

/// <summary>Đầu phiếu COA mới, độc lập với ProductInspection legacy.</summary>
public class CoaCertificate
{
    public Guid Id { get; set; }
    public Guid? EvaluationTemplateId { get; set; }
    public string? TemplateCode { get; set; }
    public string? TemplateName { get; set; }
    public int? TemplateVersion { get; set; }
    public EvaluationTemplate? EvaluationTemplate { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? CoaSpecificationId { get; set; }
    public string CertificateNo { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public DateOnly? ManufacturingDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public CoaCertificateStatus Status { get; set; }
    public string? Notes { get; set; }
    public Guid? IssuedBy { get; set; }
    public DateTime? IssuedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public CoaSpecification? CoaSpecification { get; set; }
    public ICollection<CoaCertificateResult> Results { get; set; } = new List<CoaCertificateResult>();
    public virtual Company Company { get; set; } = null!;
    public virtual Employee CreatedByEmployee { get; set; } = null!;
    public virtual Employee? IssuedByEmployee { get; set; }
}
