using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal sealed partial class ManufacturingAdjustmentService(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IPLMFieldVisibilityService visibility,
    IDateTimeProvider clock,
    INotificationService notifications,
    ManufacturingAdjustmentInputResolver inputs
)
{
    private string? Access(bool write) => user.IsInAnyRole(ApplicationRoleSets.PLM.ManufacturingAdjustmentExcludedRoles)
        ? "PLPUUser không được truy cập phiếu chỉnh màu."
        : ManufacturingAccess.Validate(
        user,
        permissions,
        write ? ApplicationPermissions.PLM.ManageManufacturingAdjustments : ApplicationPermissions.PLM.ViewProductionOrders,
        write
    ) ?? (!visibility.CanViewFormulaMaterials() ? "Bạn không có quyền truy cập vật tư công thức." : null);
    private Guid Company => user.CompanyId!.Value;
    private Guid Employee => user.EmployeeId!.Value;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private IQueryable<ManufacturingFormulaAdjustment> Scoped(bool tracked = false, bool includeInactive = false)
    {
        var query = db.ManufacturingFormulaAdjustments.Where(x => x.CompanyId == Company && (includeInactive || x.IsActive) && x.MfgProductionOrder.CompanyId == Company
            && x.MfgProductionOrder.IsActive);
        return tracked ? query : query.AsNoTracking();
    }

    private Task<ManufacturingFormulaAdjustment?> LoadAsync(Guid id, CancellationToken ct) => Scoped(true).Include(x => x.MfgProductionOrder).Include(x => x.ManufacturingFormula).Include(x => x.Batches)
        .ThenInclude(x => x.Items)
        .FirstOrDefaultAsync(x => x.ManufacturingFormulaAdjustmentId == id, ct);
    private async Task<ManufacturingFormula?> FormulaAsync(
        MfgProductionOrder order,
        Guid? id,
        CancellationToken ct
    )
    {
        var formulaId = id ?? await db.ProductionSelectVersions.AsNoTracking().Where(x => x.CompanyId == Company && x.MfgProductionOrderId == order.MfgProductionOrderId && x.ValidTo == null)
            .OrderByDescending(x => x.ValidFrom)
            .ThenByDescending(x => x.ProductionSelectVersionId)
            .Select(x => (Guid? )x.ManufacturingFormulaId)
            .FirstOrDefaultAsync(ct);
        if (!formulaId.HasValue || formulaId == Guid.Empty
            || !await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.CompanyId == Company && x.MfgProductionOrderId == order.MfgProductionOrderId
            && x.ManufacturingFormulaId == formulaId, ct))
        {
            return null;
        }

        await db.LockManufacturingFormulaAsync(formulaId.Value, Company, ct);
        return await db.ManufacturingFormulas.Include(x => x.ManufacturingFormulaMaterials).FirstOrDefaultAsync(x => x.ManufacturingFormulaId == formulaId && x.CompanyId == Company && x.IsActive, ct);
    }

    public async Task<OperationResult<GetManufacturingFormulaAdjustment>> SaveAsync(
        Guid orderId,
        Guid? id,
        ManufacturingAdjustmentInput request,
        CancellationToken ct
    )
    {
        var error = Access(true);
        if (error is not null)
        {
            return Fail(error);
        }

        if (orderId == Guid.Empty || id == Guid.Empty
            || request.Status.HasValue && !Enum.IsDefined(request.Status.Value)
            || request.Ranges?.Count > 100
            || request.Ranges?.Any(x => x is null) == true)
        {
            return Fail("Dữ liệu phiếu chỉnh màu không hợp lệ.");
        }

        if (request.Ranges?.Sum(x => Math.Max(0L, (long)x.BatchTo - x.BatchFrom + 1)) > 100)
        {
            return Fail("Mỗi lần lưu chỉ được tạo tối đa 100 mẻ.");
        }

        request = ManufacturingAdjustmentInputNormalization.Normalize(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            await db.LockProductionOrderAsync(orderId, Company, ct);
            var order = await db.MfgProductionOrders.FirstOrDefaultAsync(x => x.MfgProductionOrderId == orderId && x.CompanyId == Company && x.IsActive, ct);
            if (order is null)
            {
                return Fail("Không tìm thấy lệnh sản xuất trong công ty.");
            }

            var entity = id.HasValue ? await LoadAsync(id.Value, ct) : null;
            if (id.HasValue && (entity is null || entity.MfgProductionOrderId != orderId))
            {
                return Fail("Phiếu chỉnh màu không thuộc lệnh sản xuất.");
            }

            var formula = await FormulaAsync(order, request.ManufacturingFormulaId ?? entity?.ManufacturingFormulaId, ct);
            if (formula is null && (request.ManufacturingFormulaId.HasValue || request.Ranges?.Count > 0))
            {
                return Fail("VA không thuộc lệnh/không active trong công ty.");
            }

            var now = clock.Now;
            entity ??= new ManufacturingFormulaAdjustment

            {

                ManufacturingFormulaAdjustmentId = Guid.CreateVersion7(),

                MfgProductionOrderId = orderId,

                MfgProductionOrder = order,

                CompanyId = Company,

                CreatedBy = Employee,

                CreatedDate = now,

                IsActive = true

            };
            List<ManufacturingFormulaAdjustmentBatch>? batches = null;
            if (request.Ranges is { } ranges)
            {
                foreach (var range in ranges)
                {
                    error = ManufacturingAdjustmentBatchRules.ValidateRange(range, Math.Max(order.NumOfBatches ?? 0, 1));
                    if (error is not null)
                    {
                        return Fail(error);
                    }
                }

                var resolved = await inputs.ResolveAsync(Company, ranges, ct);
                if (!resolved.Success || resolved.Data is null)
                {
                    return Fail(resolved.Message ?? "Vật tư chỉnh màu không hợp lệ.");
                }

                var built = ManufacturingAdjustmentBatchRules.Build(
                    order,
                    entity.ManufacturingFormulaAdjustmentId,
                    ranges,
                    [],
                    formula?.ManufacturingFormulaMaterials.ToList() ?? [],
                    resolved.Data,
                    now,
                    Employee,
                    originalBatches: entity.Batches.Where(x => x.IsActive).ToList()
                );
                if (!built.Success || built.Data is null)
                {
                    return Fail(built.Message ?? "Khối lượng chỉnh màu không hợp lệ.");
                }

                batches = built.Data;
            }

            if (request.ManufacturingFormulaId.HasValue || !id.HasValue)
            {
                entity.ManufacturingFormulaId = formula?.ManufacturingFormulaId;
                entity.ManufacturingFormula = formula;
            }

            if (request.Note is not null)
            {
                entity.Note = Clean(request.Note);
            }

            if (id.HasValue)
            {
                entity.UpdatedBy = Employee;
                entity.UpdatedDate = now;
            }

            if (batches is not null)
            {
                foreach (var old in entity.Batches.Where(x => x.IsActive))
                {
                    old.IsActive = false;
                    old.UpdatedDate = now;
                    old.UpdatedBy = Employee;
                }

                // Release the existing partial unique batch/trial key before inserting replacements; same transaction.
                if (id.HasValue)
                {
                    await db.SaveChangesAsync(ct);
                }

                foreach (var batch in batches)
                {
                    entity.Batches.Add(batch);
                    if (id.HasValue)
                        await db.ManufacturingFormulaAdjustmentBatches.AddAsync(batch, ct);
                }
            }

            if (!id.HasValue)
            {
                await db.ManufacturingFormulaAdjustments.AddAsync(entity, ct);
            }

            ApproveSaved(entity, now);
            await NotifyAsync(entity, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<GetManufacturingFormulaAdjustment>.Ok(ToDto(entity));
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return Fail("Không thể lưu phiếu chỉnh màu. Vui lòng tải lại dữ liệu.");
        }
    }

    private void ApproveSaved(ManufacturingFormulaAdjustment entity, DateTime now)
    {
        entity.Status = nameof(AdjustmentStatus.Approved);
        foreach (var batch in entity.Batches.Where(x => x.IsActive && x.Status != nameof(AdjustmentStatus.Approved)))
        {
            batch.Status = nameof(AdjustmentStatus.Approved);
            batch.UpdatedBy = Employee;
            batch.UpdatedDate = now;
        }
    }

    private static GetManufacturingFormulaAdjustment ToDto(ManufacturingFormulaAdjustment entity)
    {
        var data = ManufacturingAdjustmentMapping.ProjectToDto(new[]
        {
            entity
        }.AsQueryable()).Single();
        var totals = MfgAdjustmentQuantityCalculator.CalculateTrialTotals(entity.Batches.Where(x => x.IsActive).SelectMany(ManufacturingAdjustmentBatchRules.ToRows))
            .ToDictionary(x => (x.BatchNo, x.TrialNo), x => x.FinalQuantityGram);
        foreach (var batch in data.Batches)
        {
            batch.FinalQuantityGram = totals.GetValueOrDefault((batch.BatchNo, batch.TrialNo));
        }

        return data;
    }

    private static OperationResult<GetManufacturingFormulaAdjustment> Fail(string message) => OperationResult<GetManufacturingFormulaAdjustment>.Fail(message);
}
