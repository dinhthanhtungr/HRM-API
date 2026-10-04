namespace HRM.Application.Features.Executive.SampleRequestPricingOverview.Dtos;

/// <summary>
/// Bộ lọc nghiệp vụ dành riêng cho Executive Sample Request Pricing Overview.
/// Các giá trị cũ giữ nguyên numeric value để tương thích client hiện tại.
/// </summary>
public enum SampleRequestPricingOverviewView
{
    NeedsPricing = 0,
    Draft = 10,
    Approved = 20,
    All = 30,
    MaterialCostChanged = 40,
    ProductionMaterialCostChanged = 50,
    FormulaChanged = 60
}
