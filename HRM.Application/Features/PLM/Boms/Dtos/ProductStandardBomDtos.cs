namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class AssignProductStandardBomRequest
{
    public Guid BomVersionId { get; init; }
    public DateTime ValidFrom { get; init; }
    public string? Note { get; init; }
}

public sealed class ProductStandardBomVersionDto
{
    public Guid ProductStandardBomVersionId { get; init; }
    public Guid ProductId { get; init; }
    public Guid BomDefinitionId { get; init; }
    public Guid BomVersionId { get; init; }
    public string BomCode { get; init; } = string.Empty;
    public string BomName { get; init; } = string.Empty;
    public int VersionNo { get; init; }
    public DateTime ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }
    public string? Note { get; init; }
}
