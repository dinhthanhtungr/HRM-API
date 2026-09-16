using HRM.Domain.Enums.Boms;

namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class ManufacturingLossTypeDto
{
    public Guid ManufacturingLossTypeId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public LossCalculationMethod DefaultCalculationMethod { get; init; }
    public bool IsRecoverable { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CreateManufacturingLossTypeRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public LossCalculationMethod DefaultCalculationMethod { get; init; }
    public bool IsRecoverable { get; init; }
}

public sealed class PatchManufacturingLossTypeRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool ClearDescription { get; init; }
    public LossCalculationMethod? DefaultCalculationMethod { get; init; }
    public bool? IsRecoverable { get; init; }
    public bool? IsActive { get; init; }
}
