using HRM.Domain.Entities.AttachmentSchema;

namespace HRM.Domain.Entities.PrintectSchema;

/// <summary>Logo tái sử dụng, file được quản lý qua AttachmentCollection.</summary>
public class PrintLabelLogo
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid AttachmentCollectionId { get; set; }
    public AttachmentCollection AttachmentCollection { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public ICollection<PrintLabelTemplateLogo> TemplateLogos { get; set; } = new List<PrintLabelTemplateLogo>();
    public ICollection<CustomerLabelHeader> DefaultForCustomerLabels { get; set; } = new List<CustomerLabelHeader>();
}
