using HRM.Domain.Enums.Boms;

// Kept in the shared Boms DTO namespace so existing commands and controllers remain source-compatible.

namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class ManufacturingLossProfileSummaryDto
{
    public Guid ManufacturingLossProfileId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ManufacturingLossProfileStatus Status { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? Description { get; init; }
    public int ActiveRuleCount { get; init; }
}

public sealed class ManufacturingLossProfileDto
{
    public Guid ManufacturingLossProfileId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ManufacturingLossProfileStatus Status { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? ReleasedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public IReadOnlyList<ManufacturingLossProfileRuleDto> Rules { get; init; } = [];
}

public sealed class ManufacturingLossProfileRuleDto
{
    public Guid ManufacturingLossProfileRuleId { get; init; }
    public Guid ManufacturingLossTypeId { get; init; }
    public string LossTypeCode { get; init; } = string.Empty;
    public string LossTypeName { get; init; } = string.Empty;
    public Guid? MaterialId { get; init; }
    public ManufacturingLossScope Scope { get; init; }
    public string? StageCode { get; init; }
    public string? FromStageCode { get; init; }
    public string? ToStageCode { get; init; }
    public ManufacturingLossAllocationMethod AllocationMethod { get; init; }
    public LossCalculationMethod CalculationMethod { get; init; }
    public decimal? RatePercent { get; init; }
    public decimal? FixedQuantityKg { get; init; }
    public decimal? QuantityPerEventKg { get; init; }
    public int? DefaultEventCount { get; init; }
    public int SequenceNo { get; init; }
    public bool IsRecoverable { get; init; }
    public bool IncludeInMaterialRequest { get; init; }
    public bool IsActive { get; init; }
    public string? Note { get; init; }
}

public sealed class ManufacturingLossProfileRuleWriteDto
{
    public Guid ManufacturingLossTypeId { get; init; }
    public Guid? MaterialId { get; init; }
    public ManufacturingLossScope Scope { get; init; }
    public string? StageCode { get; init; }
    public string? FromStageCode { get; init; }
    public string? ToStageCode { get; init; }
    public ManufacturingLossAllocationMethod AllocationMethod { get; init; }
    public LossCalculationMethod CalculationMethod { get; init; }
    public decimal? RatePercent { get; init; }
    public decimal? FixedQuantityKg { get; init; }
    public decimal? QuantityPerEventKg { get; init; }
    public int? DefaultEventCount { get; init; }
    public int SequenceNo { get; init; }
    public bool IsRecoverable { get; init; }
    public bool IncludeInMaterialRequest { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Note { get; init; }
}

public sealed class UpsertManufacturingLossProfileRequest
{
    public string Name { get; init; } = string.Empty;
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<ManufacturingLossProfileRuleWriteDto> Rules { get; init; } = [];
}

public sealed class ApplyManufacturingLossProfileRequest
{
    public Guid ProfileId { get; init; }
}

public sealed class ManufacturingLossProfileApplicationDto
{
    public Guid BomVersionId { get; init; }
    public Guid ProfileId { get; init; }
    public string ProfileCode { get; init; } = string.Empty;
    public bool IsPreview { get; init; }
    public IReadOnlyList<ManufacturingBomLossRuleDto> Rules { get; init; } = [];
}
