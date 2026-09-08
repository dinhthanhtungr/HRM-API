using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class BomVersionDto
{
    public Guid BomDefinitionId { get; init; }
    public Guid BomVersionId { get; init; }
    public Guid ProductId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public BomType BomType { get; init; }
    public int VersionNo { get; init; }
    public BomVersionStatus Status { get; init; }
    public decimal BaseOutputQuantity { get; init; }
    public string OutputUnit { get; init; } = string.Empty;
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? ChangeReason { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<BomItemDto> Items { get; init; } = [];
}
