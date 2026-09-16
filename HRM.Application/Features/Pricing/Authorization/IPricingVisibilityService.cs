namespace HRM.Application.Features.Pricing.Authorization;

/// <summary>
/// Cung cấp một quyết định visibility thống nhất cho mọi DTO/API có dữ liệu pricing.
/// </summary>
public interface IPricingVisibilityService
{
    PricingAccessDecision GetAccess();
}
