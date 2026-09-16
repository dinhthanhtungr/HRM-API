namespace HRM.Domain.Entities.PrintectSchema;

/// <summary>Logo được phép chọn cho một mẫu nhãn.</summary>
public class PrintLabelTemplateLogo
{
    public Guid Id { get; set; }
    public Guid PrintLabelTemplateId { get; set; }
    public virtual PrintLabelTemplate Template { get; set; } = null!;
    public Guid PrintLabelLogoId { get; set; }
    public virtual PrintLabelLogo Logo { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
