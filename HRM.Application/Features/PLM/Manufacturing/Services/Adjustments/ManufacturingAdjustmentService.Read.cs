using HRM.Application.Commons.Models;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Queries.ListManufacturingAdjustments;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal sealed partial class ManufacturingAdjustmentService
{
    public async Task<OperationResult<GetManufacturingFormulaAdjustment>> GetAsync(Guid id, CancellationToken ct)
    {
        var error = Access(false);
        if (error is not null)
        {
            return Fail(error);
        }

        var data = await ManufacturingAdjustmentMapping.ProjectToDto(Scoped()).FirstOrDefaultAsync(x => x.ManufacturingFormulaAdjustmentId == id, ct);
        if (data is null)
        {
            return Fail("Không tìm thấy phiếu chỉnh màu trong công ty.");
        }

        Calculate(data);
        return OperationResult<GetManufacturingFormulaAdjustment>.Ok(data);
    }

    public async Task<OperationResult<List<GetManufacturingFormulaAdjustment>>> ListAsync(
        ListManufacturingAdjustmentsQuery request,
        CancellationToken ct
    )
    {
        var error = Access(false);
        if (error is not null)
        {
            return OperationResult<List<GetManufacturingFormulaAdjustment>>.Fail(error);
        }

        if (request.Page <= 0 || request.PageSize is < 1 or > 100
            || request.BatchNo is <= 0 || request.TrialNo is <= 0)
        {
            return OperationResult<List<GetManufacturingFormulaAdjustment>>.Fail("Lệnh/phân trang không hợp lệ.");
        }

        if (request.IncludeInactive && !permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingAdjustments))
        {
            return OperationResult<List<GetManufacturingFormulaAdjustment>>.Fail("Không có quyền xem phiếu chỉnh màu đã xóa.");
        }

        var query = Scoped(includeInactive: request.IncludeInactive);
        if (request.OrderId is { } orderId && orderId != Guid.Empty)
            query = query.Where(x => x.MfgProductionOrderId == orderId);
        if (request.FormulaId is { } formulaId && formulaId != Guid.Empty)
            query = query.Where(x => x.ManufacturingFormulaId == formulaId);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<AdjustmentStatus>((request.Status.Trim().Equals("Approve", StringComparison.OrdinalIgnoreCase) ? nameof(AdjustmentStatus.Approved) : request.Status.Trim()), true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return OperationResult<List<GetManufacturingFormulaAdjustment>>.Fail("Trạng thái không hợp lệ.");
            }

            var normalized = parsed.ToString();
            query = parsed == AdjustmentStatus.Approved || parsed == AdjustmentStatus.Accepted
                ? query.Where(x => x.Status == nameof(AdjustmentStatus.Approved) || x.Status == "Approve" || x.Status == nameof(AdjustmentStatus.Accepted))
                : query.Where(x => x.Status == normalized);
        }

        if (request.BatchNo.HasValue)
            query = query.Where(x => x.Batches.Any(b => b.BatchNo == request.BatchNo.Value));
        if (request.TrialNo.HasValue)
            query = query.Where(x => x.Batches.Any(b => b.TrialNo == request.TrialNo.Value));
        if (request.IsAdditionalBatch.HasValue)
            query = query.Where(x => x.Batches.Any(b => b.IsAdditionalBatch == request.IsAdditionalBatch.Value));
        if (!string.IsNullOrWhiteSpace(request.BatchStatus))
        {
            if (!Enum.TryParse<AdjustmentStatus>((request.BatchStatus.Trim().Equals("Approve", StringComparison.OrdinalIgnoreCase) ? nameof(AdjustmentStatus.Approved) : request.BatchStatus.Trim()), true, out var batchStatus) || !Enum.IsDefined(batchStatus))
                return OperationResult<List<GetManufacturingFormulaAdjustment>>.Fail("Trạng thái mẻ không hợp lệ.");
            var normalized = batchStatus.ToString();
            query = batchStatus == AdjustmentStatus.Approved || batchStatus == AdjustmentStatus.Accepted
                ? query.Where(x => x.Batches.Any(b => b.Status == nameof(AdjustmentStatus.Approved) || b.Status == "Approve" || b.Status == nameof(AdjustmentStatus.Accepted)))
                : query.Where(x => x.Batches.Any(b => b.Status == normalized));
        }

        var skip = (long)(request.Page - 1) * request.PageSize;
        if (skip > int.MaxValue)
        {
            return OperationResult<List<GetManufacturingFormulaAdjustment>>.Fail("Trang vượt giới hạn.");
        }

        var data = await ManufacturingAdjustmentMapping.ProjectToDto(query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.ManufacturingFormulaAdjustmentId)
            .Skip((int)skip)
            .Take(request.PageSize))
            .ToListAsync(ct);
        foreach (var item in data)
        {
            Calculate(item);
        }

        return OperationResult<List<GetManufacturingFormulaAdjustment>>.Ok(data);
    }

    public async Task<OperationResult<GetManufacturingFormulaAdjustmentBatchGroups>> GroupsAsync(
        Guid id,
        bool byOrder,
        CancellationToken ct
    )
    {
        var error = Access(false);
        if (error is not null)
        {
            return OperationResult<GetManufacturingFormulaAdjustmentBatchGroups>.Fail(error);
        }

        if (byOrder)
        {
            id = await Scoped().Where(x => x.MfgProductionOrderId == id).OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.ManufacturingFormulaAdjustmentId)
                .Select(x => x.ManufacturingFormulaAdjustmentId)
                .FirstOrDefaultAsync(ct);
        }

        var result = await GetAsync(id, ct);
        if (!result.Success || result.Data is null)
        {
            return OperationResult<GetManufacturingFormulaAdjustmentBatchGroups>.Fail(result.Message ?? "Không tìm thấy phiếu.");
        }

        return OperationResult<GetManufacturingFormulaAdjustmentBatchGroups>.Ok(ManufacturingAdjustmentMapping.ToBatchGroupsResponse(result.Data));
    }

    private static void Calculate(GetManufacturingFormulaAdjustment data)
    {
        var totals = MfgAdjustmentQuantityCalculator.CalculateTrialTotals(ReadQuantityRows(data))
            .ToDictionary(x => (x.BatchNo, x.TrialNo), x => x.FinalQuantityGram);
        foreach (var batch in data.Batches)
        {
            batch.FinalQuantityGram = totals.GetValueOrDefault((batch.BatchNo, batch.TrialNo));
        }
    }

    private static IEnumerable<MfgAdjustmentQuantityRow> ReadQuantityRows(GetManufacturingFormulaAdjustment data) =>
        data.Batches.SelectMany(b => b.Items.Select(i => new MfgAdjustmentQuantityRow(
            b.BatchNo,
            b.TrialNo,
            b.IsAdditionalBatch,
            b.AdjustmentType,
            b.BatchQuantity,
            ManufacturingAdjustmentBatchRules.IsMaterial(i.itemType) ? $"M:{i.MaterialId:N}" : $"P:{i.ProductId:N}",
            i.BaseQuantity,
            i.AdjustedQuantity,
            i.DeltaQuantity,
            i.Unit
        )));
}
