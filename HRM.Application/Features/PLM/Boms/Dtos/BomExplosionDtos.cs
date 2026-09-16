using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class BomExplosionDto
{
    public Guid BomVersionId { get; init; }
    public Guid ProductId { get; init; }
    public decimal RequestedOutputQuantity { get; init; }
    public string OutputUnit { get; init; } = string.Empty;
    public IReadOnlyList<BomExplosionItemDto> Items { get; init; } = [];
}

public sealed class BomExplosionItemDto
{
    public int Level { get; init; }
    public int LineNo { get; init; }
    public ItemType ItemType { get; init; }
    public Guid ItemId { get; init; }
    public string? ItemCode { get; init; }
    public string? ItemName { get; init; }
    public decimal RequiredQuantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public Guid? ExpandedFromBomVersionId { get; init; }
    public IReadOnlyList<BomExplosionItemDto> Children { get; init; } = [];
}
