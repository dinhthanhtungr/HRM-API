using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Boms.Dtos;

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
