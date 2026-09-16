namespace HRM.Application.Features.PLM.ProductionOrders.Dtos;

/// <summary>
/// Kết quả kiểm tra Product và các mã ManufacturingFormula hiện đang tham gia sản xuất.
/// </summary>
public sealed class ProductInProductionDto
{
    public bool IsInProduction { get; init; }

    public IReadOnlyList<string> ManufacturingFormulaExternalIds { get; init; } = [];
}
