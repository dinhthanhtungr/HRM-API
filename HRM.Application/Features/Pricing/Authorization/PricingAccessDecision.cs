namespace HRM.Application.Features.Pricing.Authorization;

/// <summary>
/// Quyết định field-level và action-level cho dữ liệu pricing của current user.
/// Company scope, ownership và quyền trên record cụ thể vẫn phải được kiểm tra riêng.
/// </summary>
public sealed record PricingAccessDecision(
    bool CanViewWorkbench,
    bool CanViewApprovedSellingPrice,
    bool CanViewSystemCalculatedPrice,
    bool CanViewMaterialCost,
    bool CanViewManufacturingCost,
    bool CanViewMargin,
    bool CanViewHistory,
    bool CanManage,
    bool CanApprove);
