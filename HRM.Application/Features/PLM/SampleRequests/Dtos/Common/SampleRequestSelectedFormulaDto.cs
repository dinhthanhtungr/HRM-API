namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Common;

public sealed class SampleRequestSelectedFormulaDto
{
    public Guid FormulaId { get; set; }
    public string? ExternalId { get; set; }
    public string? Name { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Note { get; set; }
    public int MaterialCount { get; set; }
    public string MaterialsUrl { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } 
}
