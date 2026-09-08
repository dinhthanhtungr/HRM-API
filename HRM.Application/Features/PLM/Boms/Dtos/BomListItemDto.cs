using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Dtos;

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
