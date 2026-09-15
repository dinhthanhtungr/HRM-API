namespace HRM.Domain.Entities.PrintectSchema;

/// <summary>Một cặp key/value mặc định thuộc mẫu nhãn.</summary>
public class PrintLabelElement
{
    public Guid Id { get; set; }
    public Guid PrintLabelTemplateId { get; set; }
    public PrintLabelTemplate Template { get; set; } = null!;
    public int LineNo { get; set; }
    public string FieldKey { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public bool IsActive { get; set; } = true;
}
