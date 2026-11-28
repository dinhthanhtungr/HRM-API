using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Manufacturing.Services.Notifications;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.ProcessManufacturingFormula;

/// <summary>Xử lý VA theo hành vi Upsert cũ: chọn hiệu lực, quản lý chuẩn, snapshot trước sửa; không tự xếp lịch/reserve.</summary>
internal sealed class ProcessManufacturingFormulaCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider clock,
    IEventLogWriter timeline,
    ManufacturingFormulaInputResolver inputResolver,
    ManufacturingWorkflowNotifier notifier
) : IRequestHandler<ProcessManufacturingFormulaCommand, OperationResult<ProcessManufacturingFormulaResult>>
{
    public async Task<OperationResult<ProcessManufacturingFormulaResult>> Handle(ProcessManufacturingFormulaCommand command, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ManageManufacturingFormulas, true);
        if (error is not null)
        {
            return Fail(error);
        }

        if (command.Id == Guid.Empty || command.FormulaId == Guid.Empty)
        {
            return Fail("Lệnh/công thức không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            var productId = await db.MfgProductionOrders.AsNoTracking().Where(x => x.MfgProductionOrderId == command.Id && x.CompanyId == company && x.IsActive)
                .Select(x => (Guid?)x.ProductId)
                .FirstOrDefaultAsync(ct);
            if (!productId.HasValue)
            {
                return Fail("Không tìm thấy lệnh sản xuất trong công ty.");
            }

            await db.LockManufacturingProductAsync(productId.Value, company, ct);
            await db.LockProductionOrderAsync(command.Id, company, ct);
            await db.LockManufacturingFormulaAsync(command.FormulaId, company, ct);
            var link = await db.MfgOrderPOs.Where(x => x.MfgProductionOrderId == command.Id && x.IsActive && x.ProductionOrder.CompanyId == company
                && x.ProductionOrder.IsActive
                && x.Detail.IsActive
                && x.Detail.MerchandiseOrder.CompanyId == company
                && x.Detail.MerchandiseOrder.IsActive)
                .Include(x => x.ProductionOrder)
                .Include(x => x.Detail)
                .FirstOrDefaultAsync(ct);
            if (link is null)
            {
                return Fail("Không tìm thấy liên kết đơn hàng của lệnh.");
            }

            var order = link.ProductionOrder;
            error = ManufacturingAccess.ValidateInformState(user, order.Status);
            if (error is not null) return Fail(error);
            error = await ManufacturingAccess.FormulaEditLockAsync(db, order, ct);
            if (error is not null && command.Request.Items is not null) return Fail(error);
            if (error is not null && !await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.CompanyId == company
                && x.MfgProductionOrderId == command.Id && x.ManufacturingFormulaId == command.FormulaId && x.ValidTo == null
                && x.ValidFrom != null, ct)) return Fail(error);
            var waitingForSample = order.Status == nameof(ManufacturingProductOrder.WaitingForStandardSample);
            if (waitingForSample && (!user.IsInRole(ApplicationRoles.Lab.LabUser)
                || !command.Request.IsStandard.HasValue || command.Request.Items is not null || command.Request.Note is not null || command.Request.ClearNote))
                return Fail("Lệnh đang chờ Sale xác nhận mẫu chuẩn.");
            // Candidate/current link proves product association; a bare FormulaId is insufficient.
            var associated = await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.MfgProductionOrderId == command.Id
                && x.ManufacturingFormulaId == command.FormulaId, ct);
            if (!associated)
            {
                return Fail("Công thức không thuộc lệnh sản xuất.");
            }

            var formula = await db.ManufacturingFormulas.Where(x => x.ManufacturingFormulaId == command.FormulaId && x.CompanyId == company && x.IsActive)
                .Include(x => x.ManufacturingFormulaMaterials)
                .Include(x => x.ManufacturingFormulaVersions)
                .ThenInclude(x => x.Items)
                .FirstOrDefaultAsync(ct);
            if (formula is null)
            {
                return Fail("Không tìm thấy công thức active trong công ty.");
            }

            var request = command.Request;
            var resolved = request.Items is not null ? await inputResolver.ResolveAsync(
                order,
                new ProductionOrderFormulaOptionDto(
                    formula.ManufacturingFormulaId,
                    formula.ExternalId,
                    ManufacturingFormulaOrigin.Production,
                    command.Id,
                    formula.CreatedDate,
                    true
                ),
                request.Items,
                ct,
                allowZeroQuantity: true,
                refreshExistingPrices: true
            ) : null;
            if (resolved is not null
                && (!resolved.Success || resolved.Data is null || resolved.Data.Sum(x => x.Quantity) != 1m))
            {
                return Fail(resolved.Message ?? "Tổng tỷ lệ công thức phải bằng 1.");
            }

            var now = clock.Now;
            if (!waitingForSample)
            {
                var open = await db.ProductionSelectVersions.Where(x => x.CompanyId == company && x.MfgProductionOrderId == command.Id && x.ValidTo == null)
                    .ToListAsync(ct);
                var current = open.FirstOrDefault(x => x.ManufacturingFormulaId == command.FormulaId);
                foreach (var other in open.Where(x => x.ManufacturingFormulaId != command.FormulaId))
                {
                    other.ValidTo = now;
                    other.ClosedBy = employee;
                }

                // PostgreSQL partial unique indexes are checked per statement. Persist closures
                // before activating/inserting a different current selection inside this transaction.
                if (open.Any(x => x.ManufacturingFormulaId != command.FormulaId && x.ValidFrom != null))
                {
                    await db.SaveChangesAsync(ct);
                }

                if (current is null)
                {
                    await db.ProductionSelectVersions.AddAsync(new ProductionSelectVersion
                    {
                        ProductionSelectVersionId = Guid.CreateVersion7(),
                        MfgProductionOrderId = command.Id,
                        ManufacturingFormulaId = command.FormulaId,
                        CompanyId = company,
                        CreatedBy = employee,
                        ValidFrom = now
                    }, ct);
                }
                else if (current.ValidFrom == null)
                {
                    current.ValidFrom = now;
                }

            }
            var becomesSuccessful = string.Equals(
                order.Status,
                ManufacturingProductOrder.FormulaRequested.ToString(),
                StringComparison.OrdinalIgnoreCase
            )
                && string.Equals(
                formula.Status,
                ManufacturingProductOrderFormula.New.ToString(),
                StringComparison.OrdinalIgnoreCase
            );
            if (!waitingForSample) formula.Status = ManufacturingProductOrderFormula.Checking.ToString();
            formula.UpdatedBy = employee;
            formula.UpdatedDate = now;
            if (request.ClearNote && !string.IsNullOrWhiteSpace(request.Note))
                return Fail("Không được đồng thời xóa và nhập ghi chú VA.");
            formula.Note = ManufacturingNoteRules.FormulaNote(request.Note, request.ClearNote, formula.Note);
            if (request.IsStandard.HasValue)
            {
                var standards = await db.ProductStandardFormulas.Where(x => x.CompanyId == company && x.ProductId == order.ProductId && x.ValidTo == null)
                    .ToListAsync(ct);
                if (request.IsStandard == true && !standards.Any(x => x.ManufacturingFormulaId == command.FormulaId))
                {
                    foreach (var standard in standards)
                    {
                        standard.ValidTo = now;
                        standard.ClosedBy = employee;
                    }

                    if (standards.Count > 0)
                    {
                        await db.SaveChangesAsync(ct);
                    }

                    await db.ProductStandardFormulas.AddAsync(new ProductStandardFormula
                    {
                        ProductStandardFormulaId = Guid.CreateVersion7(),
                        ProductId = order.ProductId,
                        ManufacturingFormulaId = command.FormulaId,
                        CompanyId = company,
                        CreatedBy = employee,
                        ValidFrom = now,
                        Note = request.StandardChangeReason ?? string.Empty
                    }, ct);
                }
                else if (request.IsStandard == false)
                {
                    foreach (var standard in standards.Where(x => x.ManufacturingFormulaId == command.FormulaId))
                    {
                        standard.ValidTo = now;
                        standard.ClosedBy = employee;
                    }
                }
            }

            if (resolved?.Data is { } items)
            {
                ManufacturingFormulaVersionWriter.SnapshotBeforeEdit(formula, now, request.Note);
                ManufacturingFormulaItemWriter.Replace(formula, request.Items!, items, preserveLabUpsertBehavior: true);
            }

            if (becomesSuccessful)
            {
                order.Status = ManufacturingProductOrder.FormulaSuccess.ToString();
                order.UpdatedBy = employee;
                order.UpdatedDate = now;
                await timeline.AddAsync(new EventLogCreateRequest
                {
                    CompanyId = company,
                    EmployeeId = employee,
                    SourceType = "MfgProductionOrder",
                    SourceId = command.Id,
                    SourceCode = order.ExternalId,
                    EventType = EventType.ManufacturingProductOrder,
                    Status = order.Status,
                    CreatedDate = now,
                    Note = $"Công thức thành công vào {now} bởi {user.UserName}"
                }, ct);
            }

            var sale = await db.MerchandiseOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MerchandiseOrderId == link.Detail.MerchandiseOrderId && x.CompanyId == company && x.IsActive, ct);
            if (sale is not null)
            {
                await notifier.PriceWarningAsync(order, formula, sale, link.Detail.UnitPriceAgreed, ct);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<ProcessManufacturingFormulaResult>.Ok(new ProcessManufacturingFormulaResult(command.FormulaId, formula.Status, order.Status, true));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return Fail("Không thể xử lý công thức sản xuất. Vui lòng tải lại dữ liệu.");
        }
    }

    private static OperationResult<ProcessManufacturingFormulaResult> Fail(string message) => OperationResult<ProcessManufacturingFormulaResult>.Fail(message);
}
