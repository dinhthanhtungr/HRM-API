using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Formulas;

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

/// <summary>PATCH chỉ sửa metadata của Draft; item chỉ được thay thế bằng PUT.</summary>
public sealed class PatchBomVersionRequest
{
    public decimal? BaseOutputQuantity { get; init; }
    public string? OutputUnit { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? ChangeReason { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<string> ClearFields { get; init; } = [];
}

public sealed class BomItemWriteDto
{
    public ItemType ItemType { get; init; }
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string? Note { get; init; }
}

public sealed class BomListItemDto
{
    public Guid BomDefinitionId { get; init; }
    public Guid ProductId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public BomType BomType { get; init; }
    public bool IsActive { get; init; }
    public int VersionCount { get; init; }
}

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

public sealed class BomItemDto
{
    public Guid BomVersionItemId { get; init; }
    public int LineNo { get; init; }
    public ItemType ItemType { get; init; }
    public Guid ItemId { get; init; }
    public Guid? CategoryId { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string? ItemCode { get; init; }
    public string? ItemName { get; init; }
    public string? Note { get; init; }
}
