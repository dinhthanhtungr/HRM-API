using HRM.Domain.Enums.Manufacturings;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Domain.Entities.ManufacturingSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal sealed class ManufacturingAdjustmentDocumentService(
    IManufacturingDbContext db,
    ICurrentUser user,
    ManufacturingAdjustmentService adjustments,
    IManufacturingAdjustmentPdfRenderer renderer
)
{
    public async Task<OperationResult<byte[]>> PrintAsync(
        Guid id,
        int? batchNo,
        CancellationToken ct
    )
    {
        if (batchNo is <= 0)
        {
            return Fail("Số mẻ không hợp lệ.");
        }

        var result = await adjustments.GetAsync(id, ct);
        if (!result.Success || result.Data is null)
        {
            return Fail(result.Message ?? "Không tìm thấy phiếu.");
        }

        var data = result.Data;
        var company = user.CompanyId!.Value;
        var order = await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MfgProductionOrderId == data.MfgProductionOrderId && x.CompanyId == company && x.IsActive, ct);
        if (order is null)
        {
            return Fail("Không tìm thấy lệnh sản xuất.");
        }

        var actors = data.Batches.Select(x => x.CreatedBy).Append(data.CreatedBy).Distinct().ToArray();
        var names = await db.Employees.AsNoTracking().Where(x => x.CompanyId == company && actors.Contains(x.EmployeeId))
            .ToDictionaryAsync(x => x.EmployeeId, x => x.FullName, ct);
        await ResolveCurrentPrintLabelsAsync(data, company, ct);
        var document = Map(data, order, names);
        // Totals have already been calculated across all trials; filtering must not reset their accumulated base.
        if (batchNo.HasValue)
        {
            document.Batches = document.Batches.Where(x => x.BatchNo == batchNo).ToList();
            if (document.Batches.Count == 0)
            {
                return Fail("Không tìm thấy mẻ trong phiếu chỉnh màu.");
            }
        }

        return Render(document);
    }

    public async Task<OperationResult<byte[]>> PreviewAsync(
        Guid orderId,
        ManufacturingAdjustmentInput request,
        CancellationToken ct,
        Guid? adjustmentId = null
    )
    {
        var calculated = await adjustments.CalculateDraftAsync(orderId, request, ct, adjustmentId);
        if (!calculated.Success || calculated.Data is null)
            return Fail(calculated.Message ?? "Không tính được phiếu chỉnh màu.");
        var data = calculated.Data.Adjustment;
        // Calculate with the complete history before selecting the unsaved trials for printing.
        if (adjustmentId.HasValue)
        {
            var stored = await adjustments.GetAsync(adjustmentId.Value, ct);
            if (!stored.Success || stored.Data is null)
                return Fail(stored.Message ?? "Không tìm thấy phiếu.");
            var savedTrials = stored.Data.Batches.Select(b => (b.BatchNo, b.TrialNo)).ToHashSet();
            data.Batches = data.Batches.Where(b => !savedTrials.Contains((b.BatchNo, b.TrialNo))).ToList();
        }
        if (data.Batches.Count == 0)
            return Fail("Chưa có lần chỉnh chưa lưu để xem trước PDF.");
        var company = user.CompanyId!.Value;
        var order = await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(
            x => x.MfgProductionOrderId == orderId && x.CompanyId == company && x.IsActive, ct);
        if (order is null) return Fail("Không tìm thấy lệnh sản xuất trong công ty.");
        var actorName = await db.Employees.AsNoTracking().Where(x => x.EmployeeId == user.EmployeeId && x.CompanyId == company)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(ct) ?? user.UserName ?? string.Empty;
        await ResolveCurrentPrintLabelsAsync(data, company, ct);
        var document = Map(data, order, new Dictionary<Guid, string>
        {
            [user.EmployeeId!.Value] = actorName
        });
        foreach (var batch in document.Batches)
        {
            batch.ManufacturingFormulaAdjustmentBatchId = Guid.Empty;
        }
        return Render(document);
    }

    // Only the detached PDF DTO is enriched; persisted snapshots stay unchanged.
    private async Task ResolveCurrentPrintLabelsAsync(GetManufacturingFormulaAdjustment data, Guid company, CancellationToken ct)
    {
        var rows = data.Batches.SelectMany(b => b.Items).ToList();
        var materialIds = rows.Where(i => ManufacturingAdjustmentBatchRules.IsMaterial(i.itemType) && i.MaterialId.HasValue)
            .Select(i => i.MaterialId!.Value).Distinct().ToArray();
        var productIds = rows.Where(i => !ManufacturingAdjustmentBatchRules.IsMaterial(i.itemType) && i.ProductId.HasValue)
            .Select(i => i.ProductId!.Value).Distinct().ToArray();
        var materials = await db.Materials.AsNoTracking()
            .Where(m => m.CompanyId == company && m.IsActive == true && materialIds.Contains(m.MaterialId))
            .Select(m => new { m.MaterialId, m.ExternalId, m.Name }).ToDictionaryAsync(m => m.MaterialId, ct);
        var products = await db.Products.AsNoTracking()
            .Where(p => p.CompanyId == company && p.IsActive && productIds.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.ColourCode }).ToDictionaryAsync(p => p.ProductId, ct);
        foreach (var row in rows)
        {
            if (ManufacturingAdjustmentBatchRules.IsMaterial(row.itemType) && row.MaterialId is { } materialId &&
                materials.TryGetValue(materialId, out var material))
            {
                row.MaterialExternalIdSnapshot = material.ExternalId ?? row.MaterialExternalIdSnapshot;
                row.MaterialNameSnapshot = material.Name ?? row.MaterialNameSnapshot;
            }
            else if (!ManufacturingAdjustmentBatchRules.IsMaterial(row.itemType) && row.ProductId is { } productId &&
                products.TryGetValue(productId, out var product))
            {
                row.ProductExternalIdSnapshot = product.ColourCode ?? row.ProductExternalIdSnapshot;
            }
        }
    }

    private static PdfMfgFormulaAdjustment Map(
        GetManufacturingFormulaAdjustment data,
        MfgProductionOrder order,
        IReadOnlyDictionary<Guid, string> names
    ) => new()

    {

        ManufacturingFormulaAdjustmentId = data.ManufacturingFormulaAdjustmentId,

        ProductionOrderCode = order.ExternalId,

        ProductCode = order.ProductExternalIdSnapshot,

        ProductName = order.ProductNameSnapshot,

        FormulaCode = data.ManufacturingFormulaExternalId ?? order.FormulaExternalIdSnapshot,

        Status = data.Status,

        Note = data.Note,

        CreatedByName = names.GetValueOrDefault(data.CreatedBy),

        CreatedDate = data.CreatedDate,

        Batches = data.Batches.Select(b => new PdfMfgFormulaAdjustmentBatch
        {
            ManufacturingFormulaAdjustmentBatchId = b.ManufacturingFormulaAdjustmentBatchId,
            BatchNo = b.BatchNo,
            TrialNo = b.TrialNo,
            AdjustmentType = b.AdjustmentType,
            IsAdditionalBatch = b.IsAdditionalBatch,
            BatchQuantity = b.BatchQuantity,
            TakenQuantityGram = b.TakenQuantityGram,
            FinalQuantityGram = b.FinalQuantityGram,
            Note = b.Note,
            CreatedByName = names.GetValueOrDefault(b.CreatedBy),
            CreatedDate = b.CreatedDate,
            Items = b.Items.Select(i => new PdfMfgFormulaAdjustmentItem
            {
                CategoryId = i.CategoryId,
                Code = ManufacturingAdjustmentBatchRules.IsMaterial(i.itemType) ? i.MaterialExternalIdSnapshot : i.ProductExternalIdSnapshot,
                Name = ManufacturingAdjustmentBatchRules.IsMaterial(i.itemType) ? i.MaterialNameSnapshot : i.ProductNameSnapshot,
                BaseQuantity = i.BaseQuantity,
                AdjustedQuantity = i.AdjustedQuantity,
                DeltaQuantity = b.AdjustmentType == AdjustmentType.SetQuantity ? i.AdjustedQuantity : i.DeltaQuantity,
                Unit = i.Unit,
                LotNo = i.LotNo,
                Note = i.Note,
                LineNo = i.LineNo
            })
                .ToList()
        })
            .ToList()

    };
    private OperationResult<byte[]> Render(PdfMfgFormulaAdjustment document)
    {
        try
        {
            var bytes = renderer.Render(document);
            return bytes.Length == 0 ? Fail("Không tạo được PDF phiếu chỉnh màu.") : OperationResult<byte[]>.Ok(bytes);
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            return Fail("Không thể tạo PDF phiếu chỉnh màu.");
        }
    }

    private static OperationResult<byte[]> Fail(string message) => OperationResult<byte[]>.Fail(message);
}
