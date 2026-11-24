using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal static class ManufacturingAdjustmentInputNormalization
{
    public static ManufacturingAdjustmentInput Normalize(ManufacturingAdjustmentInput input) => new()
    {
        AppendToStoredHistory = input.AppendToStoredHistory,
        ManufacturingFormulaId = input.ManufacturingFormulaId, Status = input.Status,
        Note = input.Note, PreviewCreatedDate = input.PreviewCreatedDate,
        Ranges = input.Ranges?.Select(Normalize).ToList()
    };

    public static ManufacturingAdjustmentRangeInput Normalize(ManufacturingAdjustmentRangeInput input)
    {
        // Never aggregate malformed rows into apparently valid input.
        if (input.Items is null || input.Items.Count > 1000 || input.Items.Any(x => x is null
            || x.LotNo?.Length > 100 || x.Note?.Length > 20000
            || decimal.Round(x.QuantityGram, 3) != x.QuantityGram
            || input.AdjustmentType == AdjustmentType.SetQuantity && x.QuantityGram < 0)) return input;
        static string Lot(string? value) => string.IsNullOrWhiteSpace(value)
            || value.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase) ? "" : value.Trim().ToUpperInvariant();
        var groups = input.Items.GroupBy(x => (x.ItemId, x.ItemType, Lot: Lot(x.LotNo))).ToList();
        if (groups.Count == input.Items.Count) return input;
        List<ManufacturingAdjustmentItemInput> items;
        try
        {
            items = groups.Select(g => new ManufacturingAdjustmentItemInput
            {
                ItemId = g.Key.ItemId, ItemType = g.Key.ItemType,
                LotNo = g.First().LotNo, QuantityGram = g.Sum(x => x.QuantityGram),
                Note = string.Join("\n", g.Select(x => x.Note).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            }).ToList();
        }
        catch (OverflowException) { return input; }
        return new()
        {
            BatchFrom = input.BatchFrom, BatchTo = input.BatchTo, TrialNo = input.TrialNo,
            AdjustmentType = input.AdjustmentType, Status = input.Status,
            TakenQuantityGram = input.TakenQuantityGram, ApplyToNextBatches = input.ApplyToNextBatches,
            Note = input.Note, PreviewCreatedDate = input.PreviewCreatedDate, Items = items
        };
    }
}
