using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Versions;

public sealed class FormulaVersionDto
{
    public Guid FormulaVersionId { get; init; }
    public Guid FormulaId { get; init; }
    public int VersionNo { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Note { get; init; }
    public decimal? TotalPrice { get; init; }
    public decimal? ProductionPrice { get; init; }
    public decimal? PresidentPrice { get; init; }
    public decimal? ProfitMarginPrice { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public string? ChangeReason { get; init; }
    public IReadOnlyList<FormulaVersionItemDto>? Items { get; init; }
}

public sealed class FormulaVersionItemDto
{
    public Guid FormulaVersionItemId { get; init; }
    public int LineNo { get; init; }
    public ItemType ItemType { get; init; }
    public Guid? MaterialId { get; init; }
    public Guid? ProductId { get; init; }
    public Guid CategoryId { get; init; }
    public decimal Quantity { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? TotalPrice { get; init; }
    public string? Unit { get; init; }
    public string? MaterialExternalIdSnapshot { get; init; }
    public string? MaterialNameSnapshot { get; init; }
}

public sealed class SaveFormulaVersionRequest
{
    public string? ChangeReason { get; init; }
}

public sealed class RestoreFormulaVersionRequest
{
    public string? ChangeReason { get; init; }
    public DateTime? ExpectedUpdatedDate { get; init; }
}

public sealed class FormulaVersionActionResultDto
{
    public Guid FormulaId { get; init; }
    public Guid FormulaVersionId { get; init; }
    public int VersionNo { get; init; }
}
