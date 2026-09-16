namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class CreateBomVersionRequest
{
    public Guid SourceBomVersionId { get; init; }
    public decimal? BaseOutputQuantity { get; init; }
    public string? OutputUnit { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? ChangeReason { get; init; }
    public string? Note { get; init; }
}
