using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Devandqa;

namespace HRM.Domain.Entities.DevandqaSchema;

/// <summary>Mẫu chung theo chức năng và công ty; không bắt buộc gắn với nguyên liệu/sản phẩm.</summary>
public class EvaluationTemplate
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public EvaluationTemplateType Type { get; set; }
    public int Version { get; set; } = 1;
    public EvaluationTemplateStatus Status { get; set; }
    public bool IsDefault { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTime? PublishedDate { get; set; }
    public Company Company { get; set; } = null!;
    public Employee CreatedByEmployee { get; set; } = null!;
    public Employee? PublishedByEmployee { get; set; }
    public ICollection<EvaluationTemplateItem> Items { get; set; } = new List<EvaluationTemplateItem>();
}
