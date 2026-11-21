using System.Globalization;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal static class ManufacturingAdjustmentMapping
{
    internal static string StatusName(AdjustmentStatus status) =>
        status is AdjustmentStatus.Accepted or AdjustmentStatus.Approved
            ? nameof(AdjustmentStatus.Approved) : status.ToString();

    internal static IQueryable<GetManufacturingFormulaAdjustment> ProjectToDto(IQueryable<ManufacturingFormulaAdjustment> query)
    {
        return query.Select(x => new GetManufacturingFormulaAdjustment
        {
            ManufacturingFormulaAdjustmentId = x.ManufacturingFormulaAdjustmentId,
            MfgProductionOrderId = x.MfgProductionOrderId,
            MfgProductionOrderExternalId = x.MfgProductionOrder.ExternalId,
            ManufacturingFormulaId = x.ManufacturingFormulaId,
            ManufacturingFormulaExternalId = x.ManufacturingFormula != null && x.ManufacturingFormula.CompanyId == x.CompanyId ? x.ManufacturingFormula.ExternalId : null,
            Status = x.Status == nameof(AdjustmentStatus.Accepted) || x.Status == "Approve" ? nameof(AdjustmentStatus.Approved) : x.Status,
            Note = x.Note,
            CompanyId = x.CompanyId,
            IsActive = x.IsActive,
            CreatedDate = x.CreatedDate,
            CreatedBy = x.CreatedBy,
            UpdatedDate = x.UpdatedDate,
            UpdatedBy = x.UpdatedBy,
            Batches = x.Batches.Where(b => b.IsActive).OrderBy(b => b.BatchNo).ThenBy(b => b.TrialNo).Select(b => new GetManufacturingFormulaAdjustmentBatch
            {
                ManufacturingFormulaAdjustmentBatchId = b.ManufacturingFormulaAdjustmentBatchId,
                ManufacturingFormulaAdjustmentId = b.ManufacturingFormulaAdjustmentId,
                BatchNo = b.BatchNo,
                TrialNo = b.TrialNo,
                Status = b.Status == nameof(AdjustmentStatus.Accepted) || b.Status == "Approve" ? nameof(AdjustmentStatus.Approved) : b.Status,
                AdjustmentType = b.AdjustmentType,
                IsAdditionalBatch = b.IsAdditionalBatch,
                BatchQuantity = b.BatchQuantity,
                TakenQuantityGram = b.TakenQuantityGram,
                FinalQuantityGram = b.FinalQuantityGram,
                ApplyToNextBatches = b.ApplyToNextBatches,
                Note = b.Note,
                IsActive = b.IsActive,
                CreatedDate = b.CreatedDate,
                CreatedBy = b.CreatedBy,
                UpdatedDate = b.UpdatedDate,
                UpdatedBy = b.UpdatedBy,
                Items = b.Items.OrderBy(i => i.LineNo).Select(i => new GetManufacturingFormulaAdjustmentItem
                {
                    ManufacturingFormulaAdjustmentItemId = i.ManufacturingFormulaAdjustmentItemId,
                    ManufacturingFormulaAdjustmentBatchId = i.ManufacturingFormulaAdjustmentBatchId,
                    MaterialId = i.MaterialId,
                    ProductId = i.ProductId,
                    CategoryId = i.CategoryId,
                    itemType = i.itemType,
                    BaseQuantity = i.BaseQuantity,
                    AdjustedQuantity = i.AdjustedQuantity,
                    DeltaQuantity = i.DeltaQuantity,
                    Unit = i.Unit,
                    LotNo = i.LotNo,
                    Note = i.Note,
                    LineNo = i.LineNo,
                    MaterialNameSnapshot = i.Material == null || i.Material.CompanyId != x.CompanyId ? i.MaterialNameSnapshot : i.Material.Name,
                    MaterialExternalIdSnapshot = i.Material == null || i.Material.CompanyId != x.CompanyId ? i.MaterialExternalIdSnapshot : i.Material.ExternalId,
                    ProductNameSnapshot = i.Product == null || i.Product.CompanyId != x.CompanyId ? i.ProductNameSnapshot : FormatProductName(i.Product.ColourCode, i.Product.Name),
                    ProductExternalIdSnapshot = i.Product == null || i.Product.CompanyId != x.CompanyId ? i.ProductExternalIdSnapshot : i.Product.ColourCode
                })
                    .ToList()
            })
                .ToList()
        });
    }

    internal static GetManufacturingFormulaAdjustmentBatchGroups ToBatchGroupsResponse(GetManufacturingFormulaAdjustment adjustment)
    {
        return new GetManufacturingFormulaAdjustmentBatchGroups

        {

            ManufacturingFormulaAdjustmentId = adjustment.ManufacturingFormulaAdjustmentId,

            MfgProductionOrderId = adjustment.MfgProductionOrderId,

            MfgProductionOrderExternalId = adjustment.MfgProductionOrderExternalId,

            ManufacturingFormulaId = adjustment.ManufacturingFormulaId,

            ManufacturingFormulaExternalId = adjustment.ManufacturingFormulaExternalId,

            Status = adjustment.Status,

            Note = adjustment.Note,

            CompanyId = adjustment.CompanyId,

            IsActive = adjustment.IsActive,

            CreatedDate = adjustment.CreatedDate,

            CreatedBy = adjustment.CreatedBy,

            UpdatedDate = adjustment.UpdatedDate,

            UpdatedBy = adjustment.UpdatedBy,

            BatchGroups = GroupBatchesBySignature(adjustment.Batches)

        };
    }

    private static List<GetManufacturingFormulaAdjustmentBatchGroup> GroupBatchesBySignature(List<GetManufacturingFormulaAdjustmentBatch> batches)
    {
        var result = new List<GetManufacturingFormulaAdjustmentBatchGroup>();
        foreach (var batch in batches.OrderBy(x => x.BatchNo).ThenBy(x => x.TrialNo))
        {
            var signature = BuildBatchSignature(batch);
            var last = result.LastOrDefault();
            var canMerge = last != null && last.Signature == signature && last.BatchTo + 1 == batch.BatchNo;
            if (canMerge && last is not null)
            {
                last.BatchTo = batch.BatchNo;
                last.Batches.Add(batch);
                continue;
            }

            result.Add(new GetManufacturingFormulaAdjustmentBatchGroup
            {
                BatchFrom = batch.BatchNo,
                BatchTo = batch.BatchNo,
                Signature = signature,
                BatchTemplate = batch,
                Batches = new List<GetManufacturingFormulaAdjustmentBatch>
                {
                    batch
                }
            });
        }

        return result;
    }

    private static string BuildBatchSignature(GetManufacturingFormulaAdjustmentBatch batch)
    {
        var items = batch.Items.OrderBy(x => x.LineNo).ThenBy(x => x.MaterialId).ThenBy(x => x.ProductId).Select(x => string.Join(
            "|",
            x.itemType.ToString(),
            NormalizeGuid(x.MaterialId),
            NormalizeGuid(x.ProductId),
            NormalizeGuid(x.CategoryId),
            NormalizeDecimal(x.BaseQuantity),
            NormalizeDecimal(x.AdjustedQuantity),
            NormalizeDecimal(x.DeltaQuantity),
            NormalizeText(x.Unit),
            NormalizeText(x.LotNo)
        ));
        return string.Join(
            "::",
            batch.TrialNo,
            NormalizeText(batch.Status),
            batch.AdjustmentType.ToString(),
            batch.IsAdditionalBatch,
            NormalizeDecimal(batch.BatchQuantity),
            NormalizeDecimal(batch.TakenQuantityGram),
            NormalizeDecimal(batch.FinalQuantityGram),
            batch.ApplyToNextBatches,
            string.Join(";;", items)
        );
    }

    internal static List<ManufacturingFormulaAdjustmentBatch> FindContiguousBatchGroup(List<ManufacturingFormulaAdjustmentBatch> batches, ManufacturingFormulaAdjustmentBatch target)
    {
        var ordered = batches.OrderBy(x => x.BatchNo).ThenBy(x => x.TrialNo).ToList();
        var targetIndex = ordered.FindIndex(x => x.ManufacturingFormulaAdjustmentBatchId == target.ManufacturingFormulaAdjustmentBatchId);
        if (targetIndex < 0)
        {
            return new List<ManufacturingFormulaAdjustmentBatch>

            {

                target

            };
        }

        var signature = BuildBatchSignature(target);
        var result = new List<ManufacturingFormulaAdjustmentBatch>

        {

            target

        };
        var expectedBatchNo = target.BatchNo - 1;
        for (var i = targetIndex - 1; i >= 0; i--)
        {
            var current = ordered[i];
            if (current.BatchNo != expectedBatchNo || BuildBatchSignature(current) != signature)
            {
                break;
            }

            result.Insert(0, current);
            expectedBatchNo--;
        }

        expectedBatchNo = target.BatchNo + 1;
        for (var i = targetIndex + 1; i < ordered.Count; i++)
        {
            var current = ordered[i];
            if (current.BatchNo != expectedBatchNo || BuildBatchSignature(current) != signature)
            {
                break;
            }

            result.Add(current);
            expectedBatchNo++;
        }

        return result;
    }

    private static string BuildBatchSignature(ManufacturingFormulaAdjustmentBatch batch)
    {
        var items = batch.Items.OrderBy(x => x.LineNo).ThenBy(x => x.MaterialId).ThenBy(x => x.ProductId).Select(x => string.Join(
            "|",
            x.itemType.ToString(),
            NormalizeGuid(x.MaterialId),
            NormalizeGuid(x.ProductId),
            NormalizeGuid(x.CategoryId),
            NormalizeDecimal(x.BaseQuantity),
            NormalizeDecimal(x.AdjustedQuantity),
            NormalizeDecimal(x.DeltaQuantity),
            NormalizeText(x.Unit),
            NormalizeText(x.LotNo)
        ));
        return string.Join(
            "::",
            batch.TrialNo,
            NormalizeText(batch.Status),
            batch.AdjustmentType.ToString(),
            batch.IsAdditionalBatch,
            NormalizeDecimal(batch.BatchQuantity),
            NormalizeDecimal(batch.TakenQuantityGram),
            NormalizeDecimal(batch.FinalQuantityGram),
            batch.ApplyToNextBatches,
            string.Join(";;", items)
        );
    }

    private static string NormalizeGuid(Guid? value)
    {
        return value.HasValue && value.Value != Guid.Empty ? value.Value.ToString("N") : string.Empty;
    }

    private static string NormalizeGuid(Guid value)
    {
        return value != Guid.Empty ? value.ToString("N") : string.Empty;
    }

    private static string NormalizeDecimal(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.##########", CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string FormatProductName(string? code, string? name) => string.IsNullOrWhiteSpace(code) ? name ?? string.Empty : "[" + code + "] " + name;
}
