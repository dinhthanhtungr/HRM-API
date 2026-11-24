using HRM.Application.Commons.Models;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal sealed partial class ManufacturingAdjustmentService
{
    /// <summary>Full draft replacement; calculates without tracking, saving, locking or publishing.</summary>
    public async Task<OperationResult<ManufacturingAdjustmentCalculation>> CalculateDraftAsync(
        Guid orderId, ManufacturingAdjustmentInput request, CancellationToken ct, Guid? adjustmentId = null, bool validateDuplicates = false)
    {
        var error = Access(true);
        if (error is not null || !visibility.CanViewFormulaMaterials())
        {
            return OperationResult<ManufacturingAdjustmentCalculation>.Fail(error ?? "Không có quyền truy cập vật tư công thức.");
        }

        if (request.AppendToStoredHistory && !adjustmentId.HasValue
            || request.Ranges is null || request.Ranges.Count > 100 || request.Ranges.Any(x => x is null) || request.Note?.Length > 20000
            || request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
        {
            return OperationResult<ManufacturingAdjustmentCalculation>.Fail("Dữ liệu xem trước không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        request = ManufacturingAdjustmentInputNormalization.Normalize(request);
        if (request.Ranges is null)
            return OperationResult<ManufacturingAdjustmentCalculation>.Fail("Thiếu danh sách lần chỉnh.");
        var order = await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MfgProductionOrderId == orderId && x.CompanyId == company && x.IsActive, ct);
        if (order is null)
        {
            return OperationResult<ManufacturingAdjustmentCalculation>.Fail("Không tìm thấy lệnh sản xuất trong công ty.");
        }

        ManufacturingFormulaAdjustment? original = null;
        if (adjustmentId.HasValue)
        {
            original = await Scoped().Include(x => x.Batches).ThenInclude(x => x.Items)
                .FirstOrDefaultAsync(x => x.ManufacturingFormulaAdjustmentId == adjustmentId && x.MfgProductionOrderId == orderId, ct);
            if (original is null)
                return OperationResult<ManufacturingAdjustmentCalculation>.Fail("Không tìm thấy phiếu chỉnh màu trong lệnh/công ty.");
        }
        if (request.AppendToStoredHistory && request.Ranges.Count == 0)
        {
            var stored = await GetAsync(adjustmentId!.Value, ct);
            if (!stored.Success || stored.Data is null)
                return OperationResult<ManufacturingAdjustmentCalculation>.Fail(stored.Message ?? "Không tìm thấy phiếu.");
            stored.Data.Note = request.Note;
            if (request.Status.HasValue)
                stored.Data.Status = ManufacturingAdjustmentMapping.StatusName(request.Status.Value);
            return OperationResult<ManufacturingAdjustmentCalculation>.Ok(ToCalculation(stored.Data));
        }

        var planned = Math.Max(order.NumOfBatches ?? 0, 1);
        foreach (var range in request.Ranges)
        {
            // Read-only calculation also initializes empty input and historical rows.
            // Persisting mutations enforce the required sample weight separately.
            error = ManufacturingAdjustmentBatchRules.ValidateRange(range, planned, requireTakenQuantity: false, validateDuplicates: validateDuplicates);
            if (error is not null)
            {
                return OperationResult<ManufacturingAdjustmentCalculation>.Fail(error);
            }
        }

        if (request.Ranges.Sum(x => (long)x.BatchTo - x.BatchFrom + 1) > 100)
        {
            return OperationResult<ManufacturingAdjustmentCalculation>.Fail("Mỗi lần xem trước tối đa 100 mẻ.");
        }

        var formulaId = request.ManufacturingFormulaId ?? original?.ManufacturingFormulaId ?? await db.ProductionSelectVersions.AsNoTracking().Where(x => x.CompanyId == company && x.MfgProductionOrderId == orderId && x.ValidTo == null)
            .OrderByDescending(x => x.ValidFrom)
            .ThenByDescending(x => x.ProductionSelectVersionId)
            .Select(x => (Guid? )x.ManufacturingFormulaId)
            .FirstOrDefaultAsync(ct);
        ManufacturingFormula? formula = null;
        if (formulaId.HasValue)
        {
            if (!await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.MfgProductionOrderId == orderId && x.ManufacturingFormulaId == formulaId, ct))
            {
                return OperationResult<ManufacturingAdjustmentCalculation>.Fail("VA không thuộc lệnh sản xuất.");
            }

            formula = await ReadDraftFormulaAsync(formulaId, ct);
        }

        if (formula is null && request.Ranges.Count > 0)
        {
            return OperationResult<ManufacturingAdjustmentCalculation>.Fail("Không có VA active để tính khối lượng gốc.");
        }

        var resolved = await inputs.ResolveAsync(company, request.Ranges, ct, validateDuplicates: validateDuplicates);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResult<ManufacturingAdjustmentCalculation>.Fail(resolved.Message ?? "Vật tư không hợp lệ.");
        }

        var now = clock.Now;
        var originalBatches = original?.Batches.Where(x => x.IsActive).ToList() ?? [];
        var existing = request.AppendToStoredHistory ? originalBatches : [];
        var built = ManufacturingAdjustmentBatchRules.Build(
            order,
            Guid.Empty,
            request.Ranges,
            existing,
            formula?.ManufacturingFormulaMaterials.ToList() ?? [],
            resolved.Data,
            now,
            user.EmployeeId!.Value,
            originalBatches: request.AppendToStoredHistory ? null : originalBatches,
            requireTakenQuantity: false,
            validateDuplicates: validateDuplicates
        );
        if (!built.Success || built.Data is null)
        {
            return OperationResult<ManufacturingAdjustmentCalculation>.Fail(built.Message ?? "Khối lượng không hợp lệ.");
        }

        var entity = new ManufacturingFormulaAdjustment
        {
            MfgProductionOrderId = orderId,
            MfgProductionOrder = order,
            ManufacturingFormula = formula,
            ManufacturingFormulaId = formula?.ManufacturingFormulaId,
            CompanyId = company,
            CreatedBy = user.EmployeeId.Value,
            CreatedDate = request.PreviewCreatedDate ?? original?.CreatedDate ?? now,
            Status = ManufacturingAdjustmentMapping.StatusName(request.Status ?? AdjustmentStatus.Draft),
            Note = request.Note,
            Batches = existing.Concat(built.Data).ToList()
        };
        var batchDates = request.Ranges.SelectMany(range =>
            Enumerable.Repeat(range.PreviewCreatedDate ?? entity.CreatedDate, range.BatchTo - range.BatchFrom + 1)).ToArray();
        foreach (var (batch, date) in built.Data.Zip(batchDates))
        {
            batch.CreatedDate = date;
        }
        var data = ManufacturingAdjustmentMapping.ProjectToDto(new[]
        {
            entity
        }.AsQueryable()).Single();
        data.ManufacturingFormulaAdjustmentId = Guid.Empty;
        foreach (var batch in data.Batches)
        {
            batch.ManufacturingFormulaAdjustmentBatchId = Guid.Empty;
            batch.ManufacturingFormulaAdjustmentId = Guid.Empty;
            foreach (var item in batch.Items)
            {
                item.ManufacturingFormulaAdjustmentItemId = Guid.Empty;
                item.ManufacturingFormulaAdjustmentBatchId = Guid.Empty;
            }
        }
        return OperationResult<ManufacturingAdjustmentCalculation>.Ok(ToCalculation(data));
    }

    private static ManufacturingAdjustmentCalculation ToCalculation(GetManufacturingFormulaAdjustment data)
    {
        Calculate(data);
        var rows = ReadQuantityRows(data).ToLookup(row => row.BatchNo);
        var trials = MfgAdjustmentQuantityCalculator.CalculateTrialTotals(rows.SelectMany(group => group)).Select(total =>
            new ManufacturingAdjustmentTrialCalculation
            {
                BatchNo = total.BatchNo, TrialNo = total.TrialNo,
                OriginalBatchQuantityGram = total.OriginalBatchQuantityGram,
                NewWeighingTotalGram = total.NewWeighingTotalGram,
                ColourDeltaTotalGram = total.ColourDeltaTotalGram,
                FinalQuantityGram = total.FinalQuantityGram,
                Items = MfgAdjustmentQuantityCalculator.CalculateItemTotals(rows[total.BatchNo].Where(x => x.TrialNo <= total.TrialNo))
                    .Where(item => Guid.TryParseExact(item.ItemKey[2..], "N", out _))
                    .Select(item => new ManufacturingAdjustmentItemCalculation
                    {
                        ItemId = Guid.ParseExact(item.ItemKey[2..], "N"), IsMaterial = item.ItemKey.StartsWith("M:"),
                        OriginalBaseQuantityGram = item.OriginalBaseQuantityGram,
                        NewWeighingTotalGram = item.NewWeighingTotalGram,
                        ColourDeltaTotalGram = item.ColourDeltaTotalGram,
                        ActualQuantityGram = item.ActualQuantityGram, DeltaFromOriginalGram = item.DeltaFromOriginalGram
                    }).ToList()
            }).ToList();
        return new ManufacturingAdjustmentCalculation
        {
            Adjustment = data, Trials = trials, Groups = ManufacturingAdjustmentMapping.ToBatchGroupsResponse(data)
        };
    }

    // Editor needs quantities and identities only; never load formula cost columns.
    private Task<ManufacturingFormula?> ReadDraftFormulaAsync(Guid? formulaId, CancellationToken ct) =>
        db.ManufacturingFormulas.AsNoTracking()
            .Where(x => x.ManufacturingFormulaId == formulaId && x.CompanyId == Company && x.IsActive)
            .Select(x => new ManufacturingFormula
            {
                ManufacturingFormulaId = x.ManufacturingFormulaId,
                ExternalId = x.ExternalId, CompanyId = x.CompanyId, IsActive = x.IsActive,
                ManufacturingFormulaMaterials = x.ManufacturingFormulaMaterials.Where(i => i.IsActive)
                    .Select(i => new ManufacturingFormulaMaterial
                    {
                        MaterialId = i.MaterialId, ProductId = i.ProductId, itemType = i.itemType,
                        Quantity = i.Quantity, IsActive = i.IsActive
                    }).ToList()
            }).FirstOrDefaultAsync(ct);
}
