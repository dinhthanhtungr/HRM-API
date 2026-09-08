using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Boms.Dtos;

public sealed class BomItemWriteDto
{
    public ItemType ItemType { get; init; }
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string? Note { get; init; }
}
