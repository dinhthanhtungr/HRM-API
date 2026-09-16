namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class GenerateManufacturingFormulaFromBomRequest
{
    public string ExternalId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Note { get; init; }
}

public sealed class GeneratedManufacturingFormulaDto
{
    public Guid ManufacturingFormulaId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public Guid SourceBomVersionId { get; init; }
    public int MaterialCount { get; init; }
}
