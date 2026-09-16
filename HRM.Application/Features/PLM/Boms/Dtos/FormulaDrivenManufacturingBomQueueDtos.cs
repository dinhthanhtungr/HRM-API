using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Dtos;

/// <summary>Work queue cho Process: Formula selected nào đang cần hoàn thiện hoặc release M-BOM.</summary>
public sealed class FormulaDrivenManufacturingBomQueueItemDto
{
    public Guid FormulaId { get; init; }
    public string FormulaExternalId { get; init; } = string.Empty;
    public string FormulaName { get; init; } = string.Empty;
    public Guid ProductId { get; init; }
    public Guid? BomDefinitionId { get; init; }
    public Guid? BomVersionId { get; init; }
    public int? VersionNo { get; init; }
    public BomVersionStatus? Status { get; init; }
    public bool NeedsProcessConfiguration { get; init; }
}
