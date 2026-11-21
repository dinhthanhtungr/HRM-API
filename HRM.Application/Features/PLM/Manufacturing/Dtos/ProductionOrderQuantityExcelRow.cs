namespace HRM.Application.Features.PLM.Manufacturing.Dtos;

public sealed class ProductionOrderQuantityExcelRow
{
    public string TP { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public string ColorCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public decimal StdQuantity { get; set; }
    public decimal RequiredQuantity { get; set; }
    public decimal? UnitPrice { get; set; }
}
