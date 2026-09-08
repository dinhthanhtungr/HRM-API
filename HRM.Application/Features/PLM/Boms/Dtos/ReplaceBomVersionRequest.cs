namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class ReplaceBomVersionRequest
{
    public decimal BaseOutputQuantity { get; init; }
    public string OutputUnit { get; init; } = string.Empty;
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? ChangeReason { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<BomItemWriteDto> Items { get; init; } = [];
}
