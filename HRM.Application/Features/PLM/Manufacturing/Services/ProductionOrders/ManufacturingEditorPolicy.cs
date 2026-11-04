using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;

internal static class ManufacturingEditorPolicy
{
    public static readonly string[] HeaderFields = ["totalQuantity", "numOfBatches", "stepOfProduct", "expectedDate",
        "manufacturingDate", "bagType", "labNote", "plpuNote", "requirement", "qcCheck", "formulaNote"];

    public static readonly string[] FormulaNoteFields = ["qcCheck", "formulaNote"];

    public static string[] Actions(string status, bool canManage, bool canMaterials, bool canCreateFormula,
        bool canEditFormula = false)
    {
        if ((!canManage && !canEditFormula) || !Enum.TryParse<ManufacturingProductOrder>(status, false, out var state)
            || !Enum.IsDefined(state) || state is ManufacturingProductOrder.Unknown) return [];
        if (state == ManufacturingProductOrder.WaitingForStandardSample)
            return !canManage && canEditFormula ? ["Save"] : [];
        var actions = new List<string> { "Save" };
        if (canManage && state == ManufacturingProductOrder.Stocked) actions.Add("RollbackUseStock");
        if (state == ManufacturingProductOrder.New && !canManage) return [];
        if (canManage && (state is ManufacturingProductOrder.New or ManufacturingProductOrder.FormulaSuccess)) actions.Add("RequestFormula");
        if (canManage && state == ManufacturingProductOrder.FormulaSuccess) actions.Add("Schedule");
        if (state == ManufacturingProductOrder.New)
        {
            if (canManage) actions.Add("UseStock");
            if (canMaterials && canCreateFormula) actions.Add("SaveAndCreateFormula");
        }
        return actions.ToArray();
    }

    public static bool PlanUnchanged(HRM.Domain.Entities.ManufacturingSchema.MfgProductionOrder order,
        decimal quantity, int batches, StepOfProduct? step, DateTime? expected, DateTime? manufacturing,
        string? bagType) => quantity == (order.TotalQuantity ?? 0) && batches == (order.NumOfBatches ?? 0)
        && step == order.StepOfProduct && expected == order.ExpectedDate && manufacturing == order.ManufacturingDate
        && (bagType is null || bagType == order.BagType);

    public static IReadOnlyList<ManufacturingStepOption> Steps { get; } = Enum.GetValues<StepOfProduct>()
        .Select(x => new ManufacturingStepOption((int)x, x.ToString())).ToArray();
}

public sealed record ManufacturingStepOption(int Value, string Code);
