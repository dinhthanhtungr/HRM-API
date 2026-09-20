using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Domain.Enums.Materials;

namespace HRM.Application.Features.PLM.Materials.Rules;

/// <summary>
/// Canonical read rules for material purchase availability.
/// A material without an explicit availability record remains purchasable.
/// </summary>
public static class MaterialPurchaseAvailabilityRules
{
    public static MaterialPurchaseStatus ResolveStatus(MaterialPurchaseStatus? status)
        => status ?? MaterialPurchaseStatus.Available;

    public static bool IsPurchaseAvailable(MaterialPurchaseStatus? status)
        => ResolveStatus(status) != MaterialPurchaseStatus.Unavailable;

    public static MaterialPurchaseAvailabilityInfoDto Resolve(
        MaterialPurchaseStatus? status,
        string? reason,
        DateTime? effectiveFrom,
        DateTime? expectedAvailableDate)
    {
        var resolvedStatus = ResolveStatus(status);

        return new MaterialPurchaseAvailabilityInfoDto
        {
            Status = resolvedStatus,
            IsPurchaseAvailable = IsPurchaseAvailable(resolvedStatus),
            Reason = reason,
            EffectiveFrom = effectiveFrom,
            ExpectedAvailableDate = expectedAvailableDate
        };
    }
}
