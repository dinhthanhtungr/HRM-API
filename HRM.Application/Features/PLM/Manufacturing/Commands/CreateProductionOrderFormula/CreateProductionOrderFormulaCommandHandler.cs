using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Manufacturing.Services.Notifications;
using HRM.Application.Features.PLM.Manufacturing.Services.ProductionOrders;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CreateProductionOrderFormula;

/// <summary>Chỉ VU lần đầu vào Scheduling; mọi nguồn VA và VU cải tiến chờ QC. BE xác định nhánh từ nguồn hợp lệ.</summary>
internal sealed class CreateProductionOrderFormulaCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider clock,
    IExternalIdService codes,
    IEventLogWriter timeline,
    ManufacturingFormulaInputResolver inputResolver,
    ManufacturingWorkflowNotifier notifier,
    PromisedDeliveryNotifier promisedDelivery,
    ProductionOrderReservationService reservations,
    HRM.Application.Features.PLM.SaleOrders.DeliverySchedules.DeliveryScheduleService? deliverySchedules = null
) : IRequestHandler<CreateProductionOrderFormulaCommand, OperationResult<ProductionOrderFormulaCreatedDto>>
{
    public async Task<OperationResult<ProductionOrderFormulaCreatedDto>> Handle(CreateProductionOrderFormulaCommand command, CancellationToken ct)
    {
        var error = ManufacturingAccess.ValidateEditor(user, permissions);
        if (error is not null)
        {
            return Fail(error);
        }

        if (!permissions.HasPermission(ApplicationPermissions.PLM.ManageProductionOrders)
            && !permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials))
            return Fail("Bạn không có quyền sửa thành phần công thức.");
        var request = command.Request;
        if (request.Items?.Any(x => x is not null && x.ItemType is ItemType.MaterialFailure or ItemType.ProductFailure) == true
            && !permissions.HasPermission(ApplicationPermissions.PLM.ViewManufacturingDefectiveLots))
            return Fail("Bạn không có quyền sử dụng lot hàng lỗi khi tạo công thức.");
        if (command.Id == Guid.Empty || request.SourceFormulaId == Guid.Empty || !Enum.IsDefined(request.Origin)
            || request.TotalQuantity <= 0
            || request.NumOfBatches <= 0
            || request.StepOfProduct is null
            || !Enum.IsDefined(request.StepOfProduct.Value))
        {
            return Fail("Lệnh, nguồn công thức, lượng, số mẻ hoặc công đoạn không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            await db.LockProductionOrderAsync(command.Id, company, ct);
            var link = await db.MfgOrderPOs.Where(x => x.IsActive && x.MfgProductionOrderId == command.Id && x.ProductionOrder.IsActive
                && x.ProductionOrder.CompanyId == company
                && x.Detail.IsActive
                && x.Detail.MerchandiseOrder.IsActive
                && x.Detail.MerchandiseOrder.CompanyId == company)
                .Include(x => x.ProductionOrder)
                .Include(x => x.Detail)
                .OrderByDescending(x => x.Detail.ReplacementSourceManufacturingFormulaId.HasValue)
                .ThenBy(x => x.MerchandiseOrderDetailId)
                .FirstOrDefaultAsync(ct);
            if (link is null)
            {
                return Fail("Không tìm thấy lệnh sản xuất liên kết đơn hàng trong công ty.");
            }

            var order = link.ProductionOrder;
            error = ManufacturingAccess.ValidateInformState(user, order.Status);
            if (error is not null) return Fail(error);
            error = await ManufacturingAccess.FormulaEditLockAsync(db, order, ct);
            if (error is not null) return Fail(error);
            if (!permissions.HasPermission(ApplicationPermissions.PLM.ManageProductionOrders)
                && !ManufacturingEditorPolicy.PlanUnchanged(order, request.TotalQuantity, request.NumOfBatches,
                    request.StepOfProduct, request.ExpectedDate, request.ManufacturingDate, order.BagType))
                return Fail("Bạn không có quyền sửa thông tin lệnh sản xuất.");
            error = ManufacturingAccess.ValidatePlpuNoteChange(permissions, order.PlpuNote, request.PlpuNote);
            if (error is not null) return Fail(error);
            error = ManufacturingAccess.ValidateRequirementChange(permissions, order.Requirement, request.Requirement);
            if (error is not null) return Fail(error);
            if (!await ManufacturingEditorRevision.MatchesAsync(db, order, command.ExpectedRevision, ct))
                return Fail(ManufacturingEditorRevision.Conflict);
            if (order.Status != ManufacturingProductOrder.New.ToString())
            {
                return Fail("Chỉ có thể tạo công thức cho lệnh mới. Vui lòng tải lại dữ liệu.");
            }

            var sources = await ManufacturingFormulaOptionReader.ReadAsync(db, order, ct);
            var source = sources.FirstOrDefault(x => x.FormulaId == request.SourceFormulaId && x.Origin == request.Origin);
            if (source is null)
            {
                return Fail("Nguồn công thức không nằm trong lựa chọn hợp lệ của lệnh. Vui lòng tải lại dữ liệu.");
            }

            var isVu = source.Origin is ManufacturingFormulaOrigin.FromVu or ManufacturingFormulaOrigin.Improvement;
            if (!isVu)
            {
                await db.LockManufacturingFormulaAsync(source.FormulaId, company, ct);
            }

            if (await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.MfgProductionOrderId == command.Id && x.ValidTo == null, ct))
            {
                return Fail("Lệnh đã có công thức hiệu lực hoặc chờ xử lý trong nhánh đang chọn.");
            }

            var isReplacement = link.Detail.ReplacementSourceManufacturingFormulaId.HasValue;
            var items = isReplacement ? null : request.Items;
            var resolved = await inputResolver.ResolveAsync(order, source, items, ct, allowUntrackedSourceRows: !isReplacement);
            if (!resolved.Success || resolved.Data is null)
            {
                return Fail(resolved.Message ?? "Công thức không hợp lệ.");
            }
            if (resolved.Data.Any(x => x.ItemType is ItemType.MaterialFailure or ItemType.ProductFailure)
                && !permissions.HasPermission(ApplicationPermissions.PLM.ViewManufacturingDefectiveLots))
                return Fail("Bạn không có quyền sử dụng lot hàng lỗi khi tạo công thức.");

            var sourceNote = isVu ? await db.Formulas.AsNoTracking().Where(x => x.FormulaId == source.FormulaId && x.CompanyId == company)
                .Select(x => x.Note ?? x.Product.Requirement)
                .FirstOrDefaultAsync(ct) : await db.ManufacturingFormulas.AsNoTracking().Where(x => x.ManufacturingFormulaId == source.FormulaId && x.CompanyId == company)
                .Select(x => x.Note)
                .FirstOrDefaultAsync(ct);
            var note = source.Origin is ManufacturingFormulaOrigin.FromVu or ManufacturingFormulaOrigin.Improvement ? await db.Products.AsNoTracking().Where(x => x.ProductId == order.ProductId && x.CompanyId == company && x.IsActive)
                .Select(x => x.Requirement)
                .FirstOrDefaultAsync(ct) : sourceNote;
            var now = clock.Now;
            order.UpdatedBy = employee;
            order.UpdatedDate = now;
            order.CreatedDate = now; // Explicit legacy behavior, confirmed by owner.
            order.StepOfProduct = request.StepOfProduct;
            order.TotalQuantity = request.TotalQuantity;
            order.NumOfBatches = request.NumOfBatches;
            var previousExpectedDate = order.ExpectedDate;
            order.ExpectedDate = request.ExpectedDate;
            order.ManufacturingDate = request.ManufacturingDate;
            order.PlpuNote = await inputResolver.BuildPlpuNoteAsync(order, source, items, resolved.Data, request.PlpuNote ?? order.PlpuNote, ct, allowUntrackedSourceRows: !isReplacement);
            order.LabNote = ManufacturingNoteRules.MergeLab(request.LabNote ?? order.LabNote, sourceNote);
            PatchHelper.SetIfNotNull(request.Requirement, () => order.Requirement, v => order.Requirement = v);
            PatchHelper.SetIfNotNull(request.QcCheck, () => order.QcCheck, v => order.QcCheck = v);
            var vuId = isVu ? source.FormulaId : order.FormulaId;
            var vuCode = isVu ? source.ExternalId : order.FormulaExternalIdSnapshot;
            var createRequest = new CreateProductionOrderInformRequest

            {

                FormulaCustomerSelect = vuId ?? Guid.Empty,

                FormulaCustomerExternalIdSelect = vuCode,

                ManufacturingFormulaIdIsSelect = isVu ? null : source.FormulaId,

                ManufacturingFormulaExternalIdIsSelect = isVu ? null : source.ExternalId,

                FormulaItems = resolved.Data

            };
            var formula = ProductionOrderFormulaFactory.Create(
                createRequest,
                company,
                employee,
                now,
                isReplacement
                    ? await GenerateReplacementCodeAsync(company, source.ExternalId, ct)
                    : await codes.GenerateMonthlyCodeAsync(company, DocumentPrefix.VA.ToString(), ct)
            );
            formula.Name = "F001";
            formula.Note = note;
            formula.SourceType = isVu ? FormulaSource.FromVU : FormulaSource.FromVA;
            var autoSchedule = source.Origin == ManufacturingFormulaOrigin.FromVu;
            if (!autoSchedule) formula.Status = nameof(ManufacturingProductOrderFormula.New);
            await db.ManufacturingFormulas.AddAsync(formula, ct);
            var selection = ProductionOrderFormulaFactory.CreateSelection(order, formula, now);
            if (!autoSchedule) selection.ValidFrom = null;
            await db.ProductionSelectVersions.AddAsync(selection, ct);
            order.Status = autoSchedule ? nameof(ManufacturingProductOrder.Scheduling) : nameof(ManufacturingProductOrder.FormulaRequested);
            var schedule = await db.SchedualMfgs.FirstOrDefaultAsync(x => x.MfgProductionOrderId == command.Id, ct);

            if (autoSchedule && schedule is null)
                await db.SchedualMfgs.AddAsync(ProductionOrderFormulaFactory.CreateSchedule(order, now), ct);
            if (schedule is not null)
            {
                schedule.DeliveryPlanDate = order.ExpectedDate;
                schedule.Note = order.LabNote ?? order.PlpuNote;
                schedule.requirement = order.Requirement;
                if (autoSchedule)
                {
                    schedule.Status = order.Status;
                    schedule.StepOfProduct = order.StepOfProduct;
                }
            }
            if (autoSchedule)
            {
                // Dùng dữ liệu NVL vừa resolve: VA/selection Added chưa xuất hiện trong query trước SaveChanges.
                var reserved = await reservations.SyncAsync(order, resolved.Data, now, ct);
                if (!reserved.Success) return Fail(reserved.Message ?? "Không thể giữ chỗ nguyên vật liệu.");
            }
            await WriteEvent(
                order.MfgProductionOrderId,
                order.ExternalId,
                order.Status,
                EventType.ManufacturingProductOrder,
                now,
                ct
            );
            var sale = await db.MerchandiseOrders.FirstOrDefaultAsync(x => x.MerchandiseOrderId == link.Detail.MerchandiseOrderId && x.CompanyId == company && x.IsActive, ct);
            if (sale is not null)
            {
                if (!Enum.TryParse<MerchadiseStatus>(sale.Status, true, out var status))
                {
                    status = MerchadiseStatus.New;
                }

                if (status is MerchadiseStatus.New or MerchadiseStatus.Approved)
                {
                    sale.Status = MerchadiseStatus.Processing.ToString();
                    sale.UpdatedBy = employee;
                    sale.UpdatedDate = now;
                    await WriteEvent(sale.MerchandiseOrderId, sale.ExternalId, sale.Status, EventType.MerchadiseStatus, now, ct);
                }

                await notifier.PriceWarningAsync(order, formula, sale, link.Detail.UnitPriceAgreed, ct);
            }

            if (command.DeliveryPromises is { } promises)
            {
                if (deliverySchedules is null) return OperationResult<ProductionOrderFormulaCreatedDto>.Fail("Không thể lưu ngày hứa cùng yêu cầu công thức.");
                var saved = await deliverySchedules.SaveManufacturingPromisesAsync(command.Id, promises, transaction.TransactionId, ct);
                if (!saved.Success) return OperationResult<ProductionOrderFormulaCreatedDto>.Fail(saved.Message ?? "Không thể lưu ngày hứa.");
            }
            await db.SaveChangesAsync(ct);
            await promisedDelivery.PublishAsync(order.MfgProductionOrderId, order.ExternalId, previousExpectedDate, order.ExpectedDate, ct);
            await transaction.CommitAsync(ct);
            return OperationResult<ProductionOrderFormulaCreatedDto>.Ok(new ProductionOrderFormulaCreatedDto(formula.ManufacturingFormulaId, formula.ExternalId, order.Status, autoSchedule));
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return Fail("Không thể tạo công thức. Vui lòng tải lại dữ liệu và thử lại.");
        }
    }

    private Task WriteEvent(
        Guid id,
        string code,
        string status,
        EventType type,
        DateTime now,
        CancellationToken ct
    ) => timeline.AddAsync(new EventLogCreateRequest
    {
        EmployeeId = user.EmployeeId!.Value,
        CompanyId = user.CompanyId!.Value,
        SourceId = id,
        SourceCode = code,
        SourceType = type == EventType.ManufacturingProductOrder ? "MfgProductionOrder" : "MerchandiseOrder",
        EventType = type,
        Status = status,
        CreatedDate = now,
        Note = $"Cập nhật bởi hệ thống vào {now} bởi {user.UserName}"
    }, ct);
    private async Task<string> GenerateReplacementCodeAsync(Guid company, string sourceCode, CancellationToken ct)
    {
        var formulas = db.ManufacturingFormulas.AsNoTracking().Where(x => x.CompanyId == company);
        var separator = sourceCode.LastIndexOf('-');
        var candidateRoot = separator > 0 && int.TryParse(sourceCode.AsSpan(separator + 1), out _)
            ? sourceCode[..separator]
            : sourceCode;
        var root = await formulas.Where(x => x.ExternalId == candidateRoot)
            .Select(x => new { x.ManufacturingFormulaId, x.ExternalId })
            .SingleOrDefaultAsync(ct);
        if (root is not null)
        {
            await db.LockManufacturingFormulaAsync(root.ManufacturingFormulaId, company, ct);
        }

        var prefix = (root?.ExternalId ?? sourceCode) + "-";
        var pattern = prefix.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        var existingCodes = await formulas.Where(x => EF.Functions.ILike(x.ExternalId, pattern, "\\"))
            .Select(x => x.ExternalId)
            .ToListAsync(ct);
        var lastNumber = existingCodes
            .Select(code => int.TryParse(code.AsSpan(prefix.Length), out var number) ? number : 0)
            .DefaultIfEmpty()
            .Max();
        return prefix + checked(lastNumber + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static OperationResult<ProductionOrderFormulaCreatedDto> Fail(string message) => OperationResult<ProductionOrderFormulaCreatedDto>.Fail(message);
}
