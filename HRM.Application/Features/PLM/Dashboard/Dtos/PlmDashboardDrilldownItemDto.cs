namespace HRM.Application.Features.PLM.Dashboard.Dtos;

public sealed class PlmDashboardDrilldownItemDto
{
    public int No { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid RecordId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string? RequesterName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? ModelName { get; set; }
    public double? Quantity { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? ManagerName { get; set; }
}
