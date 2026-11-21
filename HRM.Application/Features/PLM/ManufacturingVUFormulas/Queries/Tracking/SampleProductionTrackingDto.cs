namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.Tracking;

public sealed class SampleProductionTrackingDto
{
    public Guid Id { get; init; }
    public Guid SampleRequestId { get; init; }
    public required string SampleRequestCode { get; init; }
    public string? ProductCode { get; init; }
    public string? ProductName { get; init; }
    public required string Status { get; init; }
    public decimal? ProductionQuantity { get; init; }
    public DateTime CreatedDate { get; init; }
}
