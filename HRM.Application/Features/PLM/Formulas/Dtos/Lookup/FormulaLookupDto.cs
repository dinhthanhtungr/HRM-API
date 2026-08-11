using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Lookup;

public sealed class FormulaLookupDto
{
    public Guid FormulaId { get; set; }
    public FormulaSource SourceType { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int MaterialCount { get; set; }
    public DateTime? CreatedDate { get; set; }
}
