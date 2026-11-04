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
        Guid orderId, ManufacturingAdjustmentInput request, CancellationToken ct, Guid? adjustmentId = null)
    {
        var error = Access(true);
        if (error is not null || !visibility.CanViewFormulaMaterials())
        {
            return OperationResult<ManufacturingAdjustmentCalculation>.Fail(error ?? "Không có quyền truy cập vật tư công thức.");
        }

        if (request.Ranges is null || request.Ranges.Count > 100 || request.Ranges.Any(x => x is null) || request.Note?.Length > 20000
            || request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
        {
            return OperationResult<ManufacturingAdjustmentCalculation>.Fail("Dữ liệu xem trước không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
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
        var planned = Math.Max(order.NumOfBatches ?? 0, 1);
        foreach (var range in request.Ranges)
        {
            // Read-only calculation also initializes empty input and historical rows.
            // Persisting mutations enforce the required sample weight separately.
            error = ManufacturingAdjustmentBatchRules.ValidateRange(range, planned, requireTakenQuantity: false);
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

        var resolved = await inputs.ResolveAsync(company, request.Ranges, ct);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResult<ManufacturingAdjustmentCalculation>.Fail(resolved.Message ?? "Vật tư không hợp lệ.");
        }

        var now = clock.Now;
        var built = ManufacturingAdjustmentBatchRules.Build(
            order,
            Guid.Empty,
            request.Ranges,
            [],
            formula?.ManufacturingFormulaMaterials.ToList() ?? [],
            resolved.Data,
            now,
            user.EmployeeId!.Value,
            originalBatches: original?.Batches.Where(x => x.IsActive).ToList(),
            requireTakenQuantity: false
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
            CreatedDate = request.PreviewCreatedDate ?? now,
            Status = (request.Status ?? AdjustmentStatus.Draft).ToString(),
            Note = request.Note,
            Batches = built.Data
        };
        var batchDates = request.Ranges.SelectMany(range =>
            Enumerable.Repeat(range.PreviewCreatedDate ?? entity.CreatedDate, range.BatchTo - range.BatchFrom + 1)).ToArray();
        foreach (var (batch, date) in entity.Batches.Zip(batchDates))
        {
            batch.CreatedDate = date;
        }
        var data = ManufacturingAdjustmentMapping.ProjectToDto(new[]
        {
            entity
        }.AsQueryable()).Single();
        var rows = built.Data.SelectMany(ManufacturingAdjustmentBatchRules.ToRows).ToList();
        var trials = MfgAdjustmentQuantityCalculator.CalculateTrialTotals(rows).Select(total =>
            new ManufacturingAdjustmentTrialCalculation
            {
                BatchNo = total.BatchNo, TrialNo = total.TrialNo,
                OriginalBatchQuantityGram = total.OriginalBatchQuantityGram,
                NewWeighingTotalGram = total.NewWeighingTotalGram,
                ColourDeltaTotalGram = total.ColourDeltaTotalGram,
                FinalQuantityGram = total.FinalQuantityGram,
                Items = MfgAdjustmentQuantityCalculator.CalculateItemTotals(rows.Where(
                    x => x.BatchNo == total.BatchNo && x.TrialNo <= total.TrialNo))
                    .Select(item => new ManufacturingAdjustmentItemCalculation
                    {
                        ItemId = Guid.ParseExact(item.ItemKey[2..], "N"), IsMaterial = item.ItemKey.StartsWith("M:"),
                        OriginalBaseQuantityGram = item.OriginalBaseQuantityGram,
                        NewWeighingTotalGram = item.NewWeighingTotalGram,
                        ColourDeltaTotalGram = item.ColourDeltaTotalGram,
                        ActualQuantityGram = item.ActualQuantityGram, DeltaFromOriginalGram = item.DeltaFromOriginalGram
                    }).ToList()
            }).ToList();
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
        return OperationResult<ManufacturingAdjustmentCalculation>.Ok(new ManufacturingAdjustmentCalculation
        {
            Adjustment = data, Trials = trials, Groups = ManufacturingAdjustmentMapping.ToBatchGroupsResponse(data)
        });
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
