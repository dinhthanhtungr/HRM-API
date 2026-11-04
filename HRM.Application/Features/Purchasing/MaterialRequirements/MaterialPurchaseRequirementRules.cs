namespace HRM.Application.Features.Purchasing.MaterialRequirements;

internal static class MaterialPurchaseRequirementRules
{
    public const string Shortage = "Shortage";
    public const string Sufficient = "Sufficient";

    public static decimal Remaining(decimal requested, decimal consumed)
        => Math.Max(0m, requested - consumed);

    public static decimal PurchaseQuantity(decimal required, decimal onHand, decimal pending)
        => Math.Max(0m, required - onHand - pending);
}
