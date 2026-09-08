namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class CreateBomRequest
{
    public Guid ProductId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal BaseOutputQuantity { get; init; }
    public string OutputUnit { get; init; } = string.Empty;
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<BomItemWriteDto> Items { get; init; } = [];
}
