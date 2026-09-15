namespace HRM.Domain.Entities.PrintectSchema;

/// <summary>Một vùng hiển thị hoặc trường nhập của mẫu nhãn.</summary>
public class PrintLabelElement
{
    public Guid Id { get; set; }
    public Guid PrintLabelTemplateId { get; set; }
    public PrintLabelTemplate Template { get; set; } = null!;
    public int LineNo { get; set; }
    public string ElementType { get; set; } = string.Empty;
    public string? FieldKey { get; set; }
    public string? DisplayName { get; set; }
    public string? ValueSource { get; set; }
    public string? DefaultValue { get; set; }
    public string? PrefixText { get; set; }
    public bool IsRequired { get; set; }
    public bool IsEditableBySales { get; set; }
    public bool IsActive { get; set; } = true;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? FontName { get; set; }
    public decimal? FontSize { get; set; }
    public string? Alignment { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public ICollection<CustomerLabelDetail> CustomerLabelDetails { get; set; } = new List<CustomerLabelDetail>();
}
