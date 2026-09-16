using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class BomDefinitionDetailDto
{
    public Guid BomDefinitionId { get; init; }
    public Guid ProductId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public BomType BomType { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<BomVersionSummaryDto> Versions { get; init; } = [];
}

public sealed class BomVersionSummaryDto
{
    public Guid BomVersionId { get; init; }
    public int VersionNo { get; init; }
    public BomVersionStatus Status { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? ReleasedDate { get; init; }
}
