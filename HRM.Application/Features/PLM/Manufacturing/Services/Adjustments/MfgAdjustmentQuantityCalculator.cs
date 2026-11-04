using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments
{
    /// <summary>
    /// Centralizes QC adjustment quantity rules. TakenQuantityGram is deliberately
    /// excluded because taking material out for colour adjustment does not change
    /// the physical batch quantity.
    /// </summary>
    public static class MfgAdjustmentQuantityCalculator
    {
        public static IReadOnlyList<MfgAdjustmentTrialTotal> CalculateTrialTotals(IEnumerable<MfgAdjustmentQuantityRow> source)
        {
            var rows = source?.ToList() ?? new List<MfgAdjustmentQuantityRow>();
            var results = new List<MfgAdjustmentTrialTotal>();
            foreach (var batchGroup in rows.GroupBy(x => x.BatchNo).OrderBy(x => x.Key))
            {
                var orderedRows = batchGroup.OrderBy(x => x.TrialNo).ThenBy(x => x.ItemKey).ToList();
                var isAdditionalBatch = orderedRows.First().IsAdditionalBatch;
                var originalBatchQuantityGram = isAdditionalBatch ? 0m : ResolveOriginalBatchQuantityGram(orderedRows);
                var hasNewWeighing = false;
                var newWeighingTotalGram = 0m;
                var colourDeltaTotalGram = 0m;
                foreach (var trialGroup in orderedRows.GroupBy(x => new { x.TrialNo, x.AdjustmentType }).OrderBy(x => x.Key.TrialNo))
                {
                    if (trialGroup.Key.AdjustmentType == AdjustmentType.SetQuantity)
                    {
                        hasNewWeighing = true;
                        newWeighingTotalGram += trialGroup.Sum(x => ToGram(x.AdjustedQuantity ?? x.DeltaQuantity ?? 0m, x.Unit));
                    }
                    else
                    {
                        colourDeltaTotalGram += trialGroup.Sum(GetColourDeltaGram);
                    }

                    var finalQuantityGram = (hasNewWeighing ? newWeighingTotalGram : originalBatchQuantityGram) + colourDeltaTotalGram;
                    results.Add(new MfgAdjustmentTrialTotal(
                    batchGroup.Key,
                    trialGroup.Key.TrialNo,
                    originalBatchQuantityGram,
                    newWeighingTotalGram,
                    colourDeltaTotalGram,
                    finalQuantityGram
                ));
                }
            }

            return results;
        }

        public static IReadOnlyList<MfgAdjustmentItemTotal> CalculateItemTotals(IEnumerable<MfgAdjustmentQuantityRow> source)
        {
            var rows = source?.ToList() ?? new List<MfgAdjustmentQuantityRow>();
            var results = new List<MfgAdjustmentItemTotal>();
            foreach (var itemGroup in rows.GroupBy(x => new { x.BatchNo, x.ItemKey }).OrderBy(x => x.Key.BatchNo).ThenBy(x => x.Key.ItemKey))
            {
                var orderedRows = itemGroup.OrderBy(x => x.TrialNo).ToList();
                var isAdditionalBatch = orderedRows.First().IsAdditionalBatch;
                var originalBaseQuantityGram = isAdditionalBatch ? 0m : orderedRows.Where(x => x.BaseQuantity.HasValue).Select(x => ToGram(x.BaseQuantity!.Value, x.Unit))
                .FirstOrDefault();
                var setRows = orderedRows.Where(x => x.AdjustmentType == AdjustmentType.SetQuantity).ToList();
                var hasNewWeighing = setRows.Count > 0;
                var newWeighingTotalGram = setRows.Sum(x => ToGram(x.AdjustedQuantity ?? x.DeltaQuantity ?? 0m, x.Unit));
                var colourDeltaTotalGram = orderedRows.Where(x => x.AdjustmentType == AdjustmentType.AddQuantity).Sum(GetColourDeltaGram);
                var actualQuantityGram = (hasNewWeighing ? newWeighingTotalGram : originalBaseQuantityGram) + colourDeltaTotalGram;
                results.Add(new MfgAdjustmentItemTotal(
                itemGroup.Key.BatchNo,
                itemGroup.Key.ItemKey,
                originalBaseQuantityGram,
                newWeighingTotalGram,
                colourDeltaTotalGram,
                actualQuantityGram,
                actualQuantityGram - originalBaseQuantityGram
            ));
            }

            return results;
        }

        private static decimal ResolveOriginalBatchQuantityGram(IReadOnlyCollection<MfgAdjustmentQuantityRow> rows)
        {
            var batchQuantity = rows.OrderBy(x => x.TrialNo).Select(x => x.BatchQuantity).FirstOrDefault(x => x.HasValue);
            if (batchQuantity.HasValue)
            {
                return batchQuantity.Value;
            }

            return rows.GroupBy(x => x.ItemKey).Sum(group =>
            {
                var firstBase = group.OrderBy(x => x.TrialNo).FirstOrDefault(x => x.BaseQuantity.HasValue);
                return firstBase == null ? 0m : ToGram(firstBase.BaseQuantity!.Value, firstBase.Unit);
            });
        }

        private static decimal GetColourDeltaGram(MfgAdjustmentQuantityRow row)
        {
            var delta = row.DeltaQuantity ?? (row.BaseQuantity.HasValue && row.AdjustedQuantity.HasValue ? row.AdjustedQuantity.Value - row.BaseQuantity.Value : 0m);
            return ToGram(delta, row.Unit);
        }

        public static decimal ToGram(decimal quantity, string? unit)
        {
            var normalizedUnit = unit?.Trim().ToUpperInvariant();
            return normalizedUnit is "KG" or "KGS" or "KILOGRAM" or "KILOGRAMS" ? quantity * 1000m : quantity;
        }
    }

    public sealed record MfgAdjustmentQuantityRow(
    int BatchNo,
    int TrialNo,
    bool IsAdditionalBatch,
    AdjustmentType AdjustmentType,
    decimal? BatchQuantity,
    string ItemKey,
    decimal? BaseQuantity,
    decimal? AdjustedQuantity,
    decimal? DeltaQuantity,
    string? Unit
);
    public sealed record MfgAdjustmentTrialTotal(
    int BatchNo,
    int TrialNo,
    decimal OriginalBatchQuantityGram,
    decimal NewWeighingTotalGram,
    decimal ColourDeltaTotalGram,
    decimal FinalQuantityGram
);
    public sealed record MfgAdjustmentItemTotal(
    int BatchNo,
    string ItemKey,
    decimal OriginalBaseQuantityGram,
    decimal NewWeighingTotalGram,
    decimal ColourDeltaTotalGram,
    decimal ActualQuantityGram,
    decimal DeltaFromOriginalGram
);
}
