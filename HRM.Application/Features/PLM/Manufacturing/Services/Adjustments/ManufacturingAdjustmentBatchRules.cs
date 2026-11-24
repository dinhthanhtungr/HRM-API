using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal static class ManufacturingAdjustmentBatchRules
{
    public static string? ValidateRange(ManufacturingAdjustmentRangeInput input, int planned, bool requireTakenQuantity = true, bool validateDuplicates = true)
    {
        if (input.BatchFrom <= 0 || input.BatchTo < input.BatchFrom
            || (long)input.BatchTo - input.BatchFrom >= 100
            || input.TrialNo is <= 0
            || !Enum.IsDefined(input.AdjustmentType)
            || !Enum.IsDefined(input.Status))
        {
            return "Khoảng mẻ/lần chỉnh/trạng thái không hợp lệ hoặc vượt 100 mẻ.";
        }

        if (input.Items is null || input.Items.Count is 0 or > 1000
            || input.Items.Any(x => x is null || x.ItemId == Guid.Empty || !Enum.IsDefined(x.ItemType) || x.LotNo?.Length > 100
            || x.Note?.Length > 20000
            || input.AdjustmentType == AdjustmentType.SetQuantity && x.QuantityGram < 0))
        {
            return "Cần vật tư hợp lệ; khối lượng cân mới phải lớn hơn hoặc bằng 0.";
        }

        if (validateDuplicates)
        {
            var seen = new Dictionary<(ItemType, Guid), int>();
            for (var index = 0; index < input.Items.Count; index++)
            {
                var item = input.Items[index];
                if (seen.TryGetValue((item.ItemType, item.ItemId), out var first))
                    return DuplicateItemMessage(input, first, index);
                seen[(item.ItemType, item.ItemId)] = index;
            }
        }

        if (requireTakenQuantity && (input.Items.Any(x => decimal.Round(x.QuantityGram, 3) != x.QuantityGram)
            || input.TakenQuantityGram is { } taken && decimal.Round(taken, 3) != taken))
            return "Khối lượng tối đa 3 số thập phân.";

        if (input.BatchFrom <= planned && input.BatchTo > planned)
        {
            return "Khoảng mẻ chứa cả mẻ chính và mẻ bổ sung. Vui lòng tách thành hai khoảng.";
        }

        if (input.AdjustmentType == AdjustmentType.AddQuantity &&
            (input.TakenQuantityGram is <= 0 || requireTakenQuantity && input.TakenQuantityGram is null))
        {
            return "Khối lượng lấy ra chỉnh phải lớn hơn 0.";
        }

        if (input.Note?.Length > 20000)
            return "Ghi chú tối đa 20000 ký tự.";

        return null;
    }

    public static string DuplicateItemMessage(ManufacturingAdjustmentRangeInput range, int first, int duplicate)
        => $"Mẻ {range.BatchFrom}–{range.BatchTo}, lần chỉnh {range.TrialNo?.ToString() ?? "mới"}: dòng {duplicate + 1} trùng vật tư/sản phẩm và loại hàng với dòng {first + 1}. Hãy xóa hoặc thay một trong hai dòng trước khi lưu/xem trước PDF.";

    public static OperationResult<List<ManufacturingFormulaAdjustmentBatch>> Build(
        MfgProductionOrder order,
        Guid adjustmentId,
        IReadOnlyList<ManufacturingAdjustmentRangeInput> ranges,
        IReadOnlyList<ManufacturingFormulaAdjustmentBatch> existing,
        IReadOnlyList<ManufacturingFormulaMaterial> formulaRows,
        IReadOnlyDictionary<(ItemType Type, Guid Id, string Lot), ManufacturingFormulaAdjustmentItem> templates,
        DateTime now,
        Guid employee,
        int? defaultTrial = null,
        IReadOnlyList<ManufacturingFormulaAdjustmentBatch>? originalBatches = null,
        bool requireTakenQuantity = true,
        bool validateDuplicates = true
    )
    {
        var planned = Math.Max(order.NumOfBatches ?? 0, 1);
        var defaultGram = (order.TotalQuantity ?? order.TotalQuantityRequest) / planned * 1000m;
        var result = new List<ManufacturingFormulaAdjustmentBatch>();
        foreach (var range in ranges)
        {
            var error = ValidateRange(range, planned, requireTakenQuantity, validateDuplicates);
            if (error is not null)
            {
                return Fail(error);
            }

            for (var batchNo = range.BatchFrom; batchNo <= range.BatchTo; batchNo++)
            {
                var prior = existing.Concat(result).Where(x => x.IsActive && x.BatchNo == batchNo).OrderBy(x => x.TrialNo)
                    .ToList();
                var baseline = existing.Concat(originalBatches ?? []).Concat(result).Where(x => x.IsActive && x.BatchNo == batchNo)
                    .OrderBy(x => x.TrialNo)
                    .ToList();
                var originalBatchGram = baseline.Where(x => x.AdjustmentType == AdjustmentType.AddQuantity && x.BatchQuantity > 0).Select(x => x.BatchQuantity)
                    .FirstOrDefault() ?? defaultGram;
                var trial = range.TrialNo ?? defaultTrial ?? (prior.Select(x => x.TrialNo).DefaultIfEmpty().Max() + 1);
                if (prior.Any(x => x.TrialNo == trial))
                {
                    return Fail($"Mẻ {batchNo} đã có lần chỉnh {trial}.");
                }

                var additional = batchNo > planned;
                var first = prior.Count == 0 || trial < prior.Min(x => x.TrialNo);
                if (additional && first && (trial != 1 || range.AdjustmentType != AdjustmentType.SetQuantity))
                {
                    return Fail($"Mẻ bổ sung {batchNo}: lần đầu phải là 1 và cân mẻ mới.");
                }

                var batch = new ManufacturingFormulaAdjustmentBatch

                {

                    ManufacturingFormulaAdjustmentBatchId = Guid.CreateVersion7(),

                    ManufacturingFormulaAdjustmentId = adjustmentId,

                    BatchNo = batchNo,

                    TrialNo = trial,

                    Status = ManufacturingAdjustmentMapping.StatusName(range.Status),

                    AdjustmentType = range.AdjustmentType,

                    IsAdditionalBatch = additional,

                    TakenQuantityGram = range.AdjustmentType == AdjustmentType.SetQuantity ? null : range.TakenQuantityGram,

                    BatchQuantity = range.AdjustmentType == AdjustmentType.AddQuantity && !additional
                        && !prior.Any(x => x.TrialNo < trial && x.AdjustmentType == AdjustmentType.SetQuantity) ? originalBatchGram : null,

                    ApplyToNextBatches = range.ApplyToNextBatches,

                    Note = Clean(range.Note),

                    IsActive = true,

                    CreatedDate = now,

                    CreatedBy = employee

                };
                foreach (var input in range.Items)
                {
                    var material = IsMaterial(input.ItemType);
                    var template = templates[(input.ItemType, input.ItemId, input.LotNo?.Trim() ?? string.Empty)];
                    var historical = prior.SelectMany(x => x.Items.Where(i => IsMaterial(i.itemType) == material && (material ? i.MaterialId : i.ProductId) == input.ItemId)
                        .Select(i => new { Batch = x, Item = i }))
                        .ToList();
                    var ratio = formulaRows.Where(x => x.IsActive && IsMaterial(x.itemType) == material
                        && (material ? x.MaterialId : x.ProductId) == input.ItemId)
                        .Sum(x => x.Quantity);
                    var originalRow = baseline.SelectMany(x => x.Items.Where(i => IsMaterial(i.itemType) == material && (material ? i.MaterialId : i.ProductId) == input.ItemId)
                        .Select(i => new { Batch = x, Item = i }))
                        .FirstOrDefault(x => x.Item.BaseQuantity.HasValue);
                    var original = additional ? 0m : originalRow is null ? ratio * defaultGram : MfgAdjustmentQuantityCalculator.ToGram(originalRow.Item.BaseQuantity!.Value, originalRow.Item.Unit);
                    var before = original;
                    var setSum = 0m;
                    var deltaSum = 0m;
                    var hasSet = false;
                    foreach (var previous in historical.Where(x => x.Batch.TrialNo < trial))
                    {
                        if (previous.Batch.AdjustmentType == AdjustmentType.SetQuantity)
                        {
                            hasSet = true;
                            setSum += MfgAdjustmentQuantityCalculator.ToGram(previous.Item.AdjustedQuantity ?? 0m, previous.Item.Unit);
                        }
                        else
                        {
                            deltaSum += MfgAdjustmentQuantityCalculator.ToGram(previous.Item.DeltaQuantity ?? (previous.Item.AdjustedQuantity ?? 0m) - (previous.Item.BaseQuantity ?? 0m), previous.Item.Unit);
                        }
                    }

                    before = (hasSet ? setSum : original) + deltaSum;
                    var isSet = range.AdjustmentType == AdjustmentType.SetQuantity;
                    batch.Items.Add(new ManufacturingFormulaAdjustmentItem
                    {
                        ManufacturingFormulaAdjustmentItemId = Guid.CreateVersion7(),
                        ManufacturingFormulaAdjustmentBatchId = batch.ManufacturingFormulaAdjustmentBatchId,
                        MaterialId = template.MaterialId,
                        ProductId = template.ProductId,
                        CategoryId = template.CategoryId,
                        itemType = string.IsNullOrWhiteSpace(input.LotNo) ||
                            string.Equals(input.LotNo.Trim(), "N/A", StringComparison.OrdinalIgnoreCase)
                            ? input.ItemType : template.itemType,
                        BaseQuantity = isSet ? original : before,
                        AdjustedQuantity = isSet ? input.QuantityGram : before + input.QuantityGram,
                        DeltaQuantity = isSet ? input.QuantityGram - original : input.QuantityGram,
                        Unit = "g",
                        LotNo = Clean(input.LotNo) ?? string.Empty,
                        Note = Clean(input.Note),
                        LineNo = batch.Items.Count + 1,
                        MaterialNameSnapshot = template.MaterialNameSnapshot,
                        MaterialExternalIdSnapshot = template.MaterialExternalIdSnapshot,
                        ProductNameSnapshot = template.ProductNameSnapshot,
                        ProductExternalIdSnapshot = template.ProductExternalIdSnapshot
                    });
                }

                if (range.AdjustmentType == AdjustmentType.SetQuantity)
                {
                    batch.BatchQuantity = batch.Items.Sum(x => x.AdjustedQuantity ?? 0);
                }

                result.Add(batch);
                // Avoid overflow when BatchTo=int.MaxValue after processing the last value.
                if (batchNo == range.BatchTo)
                {
                    break;
                }
            }
        }

        var all = existing.Concat(result).Where(x => x.IsActive).ToList();
        var changedBatchNumbers = result.Concat(originalBatches ?? []).Select(batch => batch.BatchNo).ToHashSet();
        var affected = all.Where(batch => changedBatchNumbers.Contains(batch.BatchNo));
        var sequenceError = ValidateSequence(affected, planned);
        if (sequenceError is not null)
        {
            return Fail(sequenceError);
        }

        var totals = MfgAdjustmentQuantityCalculator.CalculateTrialTotals(affected.SelectMany(ToRows)).ToDictionary(x => (x.BatchNo, x.TrialNo), x => x.FinalQuantityGram);
        foreach (var invalid in totals.Where(x => x.Value <= 0))
            return Fail($"Mẻ {invalid.Key.BatchNo}, lần chỉnh {invalid.Key.TrialNo}: khối lượng sau chỉnh phải lớn hơn 0.");
        foreach (var batch in result)
        {
            batch.FinalQuantityGram = totals.GetValueOrDefault((batch.BatchNo, batch.TrialNo));
            if (batch.FinalQuantityGram <= 0)
            {
                return Fail($"Mẻ {batch.BatchNo}: khối lượng sau chỉnh phải lớn hơn 0.");
            }
        }

        return OperationResult<List<ManufacturingFormulaAdjustmentBatch>>.Ok(result);
    }

    public static string? ValidateSequence(IEnumerable<ManufacturingFormulaAdjustmentBatch> rows, int planned)
    {
        foreach (var group in rows.Where(x => x.IsActive).GroupBy(x => x.BatchNo))
        {
            if (group.GroupBy(x => x.TrialNo).Any(x => x.Count() > 1))
            {
                return $"Mẻ {group.Key} bị trùng lần chỉnh.";
            }

            var first = group.OrderBy(x => x.TrialNo).First();
            if (group.Key > planned && (first.TrialNo != 1 || first.AdjustmentType != AdjustmentType.SetQuantity))
            {
                return $"Mẻ bổ sung {group.Key}: cần lần 1 cân mẻ mới trước các lần khác.";
            }
        }

        return null;
    }

    public static IEnumerable<MfgAdjustmentQuantityRow> ToRows(ManufacturingFormulaAdjustmentBatch batch) => batch.Items.Select(x => new MfgAdjustmentQuantityRow(
        batch.BatchNo,
        batch.TrialNo,
        batch.IsAdditionalBatch,
        batch.AdjustmentType,
        batch.BatchQuantity,
        IsMaterial(x.itemType) ? $"M:{x.MaterialId:N}" : $"P:{x.ProductId:N}",
        x.BaseQuantity,
        x.AdjustedQuantity,
        x.DeltaQuantity,
        x.Unit
    ));
    public static bool IsMaterial(ItemType type) => type is ItemType.Material or ItemType.MaterialFailure;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static OperationResult<List<ManufacturingFormulaAdjustmentBatch>> Fail(string message) => OperationResult<List<ManufacturingFormulaAdjustmentBatch>>.Fail(message);
}
