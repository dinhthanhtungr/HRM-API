using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.DevAndQA.MaterialEquivalences;

public static class MaterialDemandRules
{
    public static readonly string[] ActiveStatuses = Enum.GetValues<ManufacturingProductOrder>()
        .Where(x => x is not (ManufacturingProductOrder.Unknown or ManufacturingProductOrder.Canceled or ManufacturingProductOrder.Finished
            or ManufacturingProductOrder.Done or ManufacturingProductOrder.Stocked or ManufacturingProductOrder.Reported))
        .Select(x => x.ToString()).ToArray();
    public static decimal Remaining(decimal? requested, decimal? used) => Math.Max(0, (requested ?? 0) - (used ?? 0));
    // Available already subtracts all reservations. Add this demand back before comparing, once only.
    public static decimal Shortage(decimal demand, decimal onHand, decimal reservedAll)
        => Math.Max(0, demand - Math.Max(0, onHand - Math.Max(0, reservedAll - demand)));
}
