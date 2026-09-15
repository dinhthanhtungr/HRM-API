using HRM.Domain.Entities.AttachmentSchema;

namespace HRM.Domain.Entities.PrintectSchema;

/// <summary>Danh mục mẫu nhãn dùng chung trong một công ty.</summary>
public class PrintLabelTemplate
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LabelType { get; set; }
    public string? Instructions { get; set; }
    public decimal WidthMm { get; set; }
    public decimal HeightMm { get; set; }
    public Guid? AttachmentCollectionId { get; set; }
    public AttachmentCollection? AttachmentCollection { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public ICollection<PrintLabelElement> Elements { get; set; } = new List<PrintLabelElement>();
    public ICollection<PrintLabelTemplateLogo> TemplateLogos { get; set; } = new List<PrintLabelTemplateLogo>();
}
