using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Domain.Entities.ManufacturingSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal sealed partial class ManufacturingAdjustmentService
{
    public async Task<OperationResult<GetManufacturingFormulaAdjustment>> SaveRangeAsync(
        Guid id,
        Guid? batchId,
        ManufacturingAdjustmentRangeInput request,
        bool singleBatchUpsert,
        CancellationToken ct
    )
    {
        var error = Access(true);
        if (error is not null)
        {
            return Fail(error);
        }

        if (id == Guid.Empty || batchId == Guid.Empty
            || singleBatchUpsert && request.BatchFrom != request.BatchTo)
        {
            return Fail("Phiếu/mẻ không hợp lệ.");
        }

        request = ManufacturingAdjustmentInputNormalization.Normalize(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            var orderId = await Scoped().Where(x => x.ManufacturingFormulaAdjustmentId == id).Select(x => (Guid? )x.MfgProductionOrderId)
                .FirstOrDefaultAsync(ct);
            if (!orderId.HasValue)
            {
                return Fail("Không tìm thấy phiếu chỉnh màu trong công ty.");
            }

            await db.LockProductionOrderAsync(orderId.Value, Company, ct);
            var entity = await LoadAsync(id, ct);
            if (entity is null)
            {
                return Fail("Không tìm thấy phiếu active.");
            }

            var planned = Math.Max(entity.MfgProductionOrder.NumOfBatches ?? 0, 1);
            error = ManufacturingAdjustmentBatchRules.ValidateRange(request, planned);
            if (error is not null)
            {
                return Fail(error);
            }

            var target = batchId.HasValue ? entity.Batches.FirstOrDefault(x => x.ManufacturingFormulaAdjustmentBatchId == batchId && x.IsActive) : singleBatchUpsert && request.TrialNo.HasValue ? entity.Batches.FirstOrDefault(x => x.IsActive && x.BatchNo == request.BatchFrom && x.TrialNo == request.TrialNo) : null;
            if (batchId.HasValue && target is null)
            {
                return Fail("Mẻ không thuộc phiếu chỉnh màu.");
            }

            var replaced = target is null ? [] : singleBatchUpsert ? new List<ManufacturingFormulaAdjustmentBatch>

            {

                target

            }

            : ManufacturingAdjustmentMapping.FindContiguousBatchGroup(entity.Batches.Where(x => x.IsActive).ToList(), target);
            var replacedIds = replaced.Select(x => x.ManufacturingFormulaAdjustmentBatchId).ToHashSet();
            var existing = entity.Batches.Where(x => x.IsActive && !replacedIds.Contains(x.ManufacturingFormulaAdjustmentBatchId))
                .ToList();
            var formula = await FormulaAsync(entity.MfgProductionOrder, entity.ManufacturingFormulaId, ct);
            if (formula is null)
            {
                return Fail("VA không còn hợp lệ/không thuộc lệnh.");
            }

            var resolved = await inputs.ResolveAsync(Company, [request], ct);
            if (!resolved.Success || resolved.Data is null)
            {
                return Fail(resolved.Message ?? "Vật tư không hợp lệ.");
            }

            var now = clock.Now;
            var built = ManufacturingAdjustmentBatchRules.Build(
                entity.MfgProductionOrder,
                id,
                [request],
                existing,
                formula.ManufacturingFormulaMaterials.ToList(),
                resolved.Data,
                now,
                Employee,
                target?.TrialNo,
                replaced
            );
            if (!built.Success || built.Data is null)
            {
                return Fail(built.Message ?? "Khối lượng chỉnh màu không hợp lệ.");
            }

            foreach (var old in replaced.Where(x => !built.Data.Any(b => b.BatchNo == x.BatchNo)))
            {
                old.IsActive = false;
                old.UpdatedBy = Employee;
                old.UpdatedDate = now;
            }

            // Deactivate removed keys before inserts; preserve existing batch IDs for the retained physical batches.
            if (replaced.Any(x => !x.IsActive))
            {
                await db.SaveChangesAsync(ct);
            }

            foreach (var batch in built.Data)
            {
                var old = replaced.FirstOrDefault(x => x.BatchNo == batch.BatchNo);
                if (old is null)
                {
                    entity.Batches.Add(batch);
                    // UUID đã có sẵn: đánh dấu Added để EF INSERT thay vì UPDATE bản ghi chưa tồn tại.
                    await db.ManufacturingFormulaAdjustmentBatches.AddAsync(batch, ct);
                    continue;
                }

                old.BatchNo = batch.BatchNo;
                old.TrialNo = batch.TrialNo;
                old.Status = batch.Status;
                old.AdjustmentType = batch.AdjustmentType;
                old.IsAdditionalBatch = batch.IsAdditionalBatch;
                old.BatchQuantity = batch.BatchQuantity;
                old.TakenQuantityGram = batch.TakenQuantityGram;
                old.FinalQuantityGram = batch.FinalQuantityGram;
                old.ApplyToNextBatches = batch.ApplyToNextBatches;
                old.Note = batch.Note;
                old.UpdatedBy = Employee;
                old.UpdatedDate = now;
                db.ManufacturingFormulaAdjustmentItems.RemoveRange(old.Items);
                old.Items.Clear();
                foreach (var item in batch.Items)
                {
                    item.ManufacturingFormulaAdjustmentBatchId = old.ManufacturingFormulaAdjustmentBatchId;
                    old.Items.Add(item);
                    await db.ManufacturingFormulaAdjustmentItems.AddAsync(item, ct);
                }
            }

            if (batchId.HasValue)
            {
                entity.UpdatedDate = now;
                entity.UpdatedBy = Employee;
            }

            ApproveSaved(entity, now);

            // Legacy AddBatch (including single-batch upsert) did not publish; range create/patch did.
            if (!singleBatchUpsert)
            {
                await NotifyAsync(entity, ct);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<GetManufacturingFormulaAdjustment>.Ok(ToDto(entity));
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return Fail("Không thể lưu mẻ chỉnh màu. Vui lòng tải lại dữ liệu.");
        }
    }

    public async Task<OperationResult> DeleteAsync(
        Guid id,
        Guid? batchId,
        CancellationToken ct
    )
    {
        var error = Access(true);
        if (error is not null)
        {
            return OperationResult.Fail(error);
        }

        if (id == Guid.Empty || batchId == Guid.Empty)
        {
            return OperationResult.Fail("Phiếu/mẻ không hợp lệ.");
        }

        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            var orderId = await Scoped().Where(x => x.ManufacturingFormulaAdjustmentId == id).Select(x => (Guid? )x.MfgProductionOrderId)
                .FirstOrDefaultAsync(ct);
            if (!orderId.HasValue)
            {
                return OperationResult.Fail("Không tìm thấy phiếu chỉnh màu trong công ty.");
            }

            await db.LockProductionOrderAsync(orderId.Value, Company, ct);
            var entity = await LoadAsync(id, ct);
            if (entity is null)
            {
                return OperationResult.Fail("Không tìm thấy phiếu active.");
            }

            var now = clock.Now;
            if (batchId.HasValue)
            {
                var batch = entity.Batches.FirstOrDefault(x => x.ManufacturingFormulaAdjustmentBatchId == batchId && x.IsActive);
                if (batch is null)
                {
                    return OperationResult.Fail("Mẻ không thuộc phiếu chỉnh màu.");
                }

                error = ManufacturingAdjustmentBatchRules.ValidateSequence(entity.Batches.Where(x => x != batch), Math.Max(entity.MfgProductionOrder.NumOfBatches ?? 0, 1));
                if (error is not null)
                {
                    return OperationResult.Fail(error);
                }

                if (MfgAdjustmentQuantityCalculator.CalculateTrialTotals(entity.Batches
                    .Where(x => x.IsActive && x != batch).SelectMany(ManufacturingAdjustmentBatchRules.ToRows))
                    .Any(x => x.FinalQuantityGram <= 0))
                    return OperationResult.Fail("Không thể xóa: có lần chỉnh còn lại có khối lượng sau chỉnh không lớn hơn 0.");

                ApproveSaved(entity, now);
                batch.IsActive = false;
                batch.UpdatedDate = now;
                batch.UpdatedBy = Employee;
            }
            else
            {
                ApproveSaved(entity, now);
                entity.IsActive = false;
            }

            entity.UpdatedDate = now;
            entity.UpdatedBy = Employee;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult.Ok("Đã xóa phiếu/mẻ chỉnh màu.");
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return OperationResult.Fail("Không thể xóa phiếu/mẻ chỉnh màu.");
        }
    }
}
