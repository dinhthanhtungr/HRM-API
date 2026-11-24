using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal sealed partial class ManufacturingAdjustmentService
{
    public async Task<OperationResult<ManufacturingAdjustmentEditor>> InitializeAsync(
        Guid orderId, Guid? adjustmentId, CancellationToken ct)
    {
        var error = Access(true);
        if (error is not null) return OperationResult<ManufacturingAdjustmentEditor>.Fail(error);
        var order = await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(
            x => x.MfgProductionOrderId == orderId && x.CompanyId == Company && x.IsActive, ct);
        if (order is null) return OperationResult<ManufacturingAdjustmentEditor>.Fail("Không tìm thấy lệnh sản xuất trong công ty.");
        var planned = Math.Max(order.NumOfBatches ?? 0, 1);
        ManufacturingAdjustmentInput draft;
        ManufacturingAdjustmentCalculation? savedCalculation = null;
        if (adjustmentId.HasValue)
        {
            var data = await ManufacturingAdjustmentMapping.ProjectToDto(Scoped().Where(
                x => x.ManufacturingFormulaAdjustmentId == adjustmentId && x.MfgProductionOrderId == orderId))
                .FirstOrDefaultAsync(ct);
            if (data is null)
                return OperationResult<ManufacturingAdjustmentEditor>.Fail("Không tìm thấy phiếu chỉnh màu trong lệnh/công ty.");
            if (!TryReadStatus(data.Status, out var status) || data.Batches.Any(b => !TryReadStatus(b.Status, out _)))
                return OperationResult<ManufacturingAdjustmentEditor>.Fail("Phiếu có trạng thái lịch sử không hỗ trợ chỉnh sửa.");
            savedCalculation = ToCalculation(data);
            draft = new()
            {
                ManufacturingFormulaId = data.ManufacturingFormulaId,
                Status = status, Note = data.Note,
                PreviewCreatedDate = data.CreatedDate,
                Ranges = data.Batches.Select(b => new ManufacturingAdjustmentRangeInput
                {
                    BatchFrom = b.BatchNo, BatchTo = b.BatchNo, TrialNo = b.TrialNo,
                    AdjustmentType = b.AdjustmentType, Status = Enum.Parse<AdjustmentStatus>(b.Status),
                    TakenQuantityGram = b.TakenQuantityGram, ApplyToNextBatches = b.ApplyToNextBatches,
                    Note = b.Note, PreviewCreatedDate = b.CreatedDate,
                    Items = b.Items.Select(i => new ManufacturingAdjustmentItemInput
                    {
                        ItemId = (ManufacturingAdjustmentBatchRules.IsMaterial(i.itemType) ? i.MaterialId : i.ProductId) ?? Guid.Empty,
                        ItemType = i.itemType,
                        QuantityGram = MfgAdjustmentQuantityCalculator.ToGram(
                            (b.AdjustmentType == AdjustmentType.SetQuantity ? i.AdjustedQuantity
                                : i.DeltaQuantity ?? (i.AdjustedQuantity ?? 0m) - (i.BaseQuantity ?? 0m)) ?? 0m, i.Unit),
                        LotNo = i.LotNo, Note = i.Note
                    }).ToList()
                }).ToList()
            };
        }
        else
        {
            var formulaId = await db.ProductionSelectVersions.AsNoTracking()
                .Where(x => x.CompanyId == Company && x.MfgProductionOrderId == orderId && x.ValidTo == null)
                .OrderByDescending(x => x.ValidFrom).ThenByDescending(x => x.ProductionSelectVersionId)
                .Select(x => (Guid?)x.ManufacturingFormulaId).FirstOrDefaultAsync(ct);
            var formula = await ReadDraftFormulaAsync(formulaId, ct);
            if (formula is null) return OperationResult<ManufacturingAdjustmentEditor>.Fail("Không có VA active để khởi tạo.");
            draft = new()
            {
                ManufacturingFormulaId = formula.ManufacturingFormulaId, Status = AdjustmentStatus.Draft,
                Ranges = [new()
                {
                    BatchFrom = 1, BatchTo = planned > 100 ? 1 : planned, TrialNo = 1, AdjustmentType = AdjustmentType.AddQuantity,
                    Items = formula.ManufacturingFormulaMaterials.Where(x => x.IsActive)
                        .GroupBy(x => new { x.itemType, Id = ManufacturingAdjustmentBatchRules.IsMaterial(x.itemType) ? x.MaterialId : x.ProductId })
                        .Select(g => new ManufacturingAdjustmentItemInput
                        {
                            ItemId = g.Key.Id ?? Guid.Empty, ItemType = g.Key.itemType, QuantityGram = 0m
                        }).ToList()
                }]
            };
        }
        draft = ManufacturingAdjustmentInputNormalization.Normalize(draft);
        if (savedCalculation is null)
        {
            var calculation = await CalculateDraftAsync(orderId, draft, ct);
            if (!calculation.Success || calculation.Data is null)
                return OperationResult<ManufacturingAdjustmentEditor>.Fail(calculation.Message ?? "Không khởi tạo được phiếu.");
            savedCalculation = calculation.Data;
        }
        return OperationResult<ManufacturingAdjustmentEditor>.Ok(new ManufacturingAdjustmentEditor()
        {
            SourceAdjustmentId = adjustmentId, PlannedBatchCount = planned,
            PlannedBatchQuantityGram = (order.TotalQuantity ?? order.TotalQuantityRequest) / planned * 1000m,
            Draft = draft, Calculation = savedCalculation
        });
    }

    private static bool TryReadStatus(string value, out AdjustmentStatus status) =>
        Enum.TryParse(value, out status) && Enum.IsDefined(status);
}
