using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.ProductionOrders.Dtos;

public sealed class CreateProductionOrderFormulaItemRequest
{
    public Guid ItemId { get; set; }
    public ItemType ItemType { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? MaterialNameSnapshot { get; set; }
    public string? MaterialExternalIdSnapshot { get; set; }
    public ProductionOrderLotNumberDto? LotNumber { get; set; }
    public string? Unit { get; set; }
    public bool IsActive { get; set; } = true;
    public int LineNo { get; set; }
}
