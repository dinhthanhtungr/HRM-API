namespace HRM.Domain.Entities.CustomerSchema;

public sealed class QuotationTerm
{
    public Guid QuotationTermId { get; set; }
    public Guid QuotationId { get; set; }

    public string LabelVi { get; set; } = string.Empty;
    public string? LabelEn { get; set; }
    public string? ValueVi { get; set; }
    public string? ValueEn { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Quotation Quotation { get; set; } = null!;
}
