using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CopyProductionOrder;

internal sealed class CopyProductionOrderCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IExternalIdService codes,
    IDateTimeProvider clock,
    IEventLogWriter timeline,
    ManufacturingFormulaInputResolver inputs,
    ProductionOrderReservationService reservations
) : IRequestHandler<CopyProductionOrderCommand, OperationResult<CreateProductionOrderInformResult>>
{
    public async Task<OperationResult<CreateProductionOrderInformResult>> Handle(CopyProductionOrderCommand command, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.CreateProductionOrders, true);
        if (error is not null)
        {
            return Fail(error);
        }

        var input = command.Request;
        if (input.Items is not null && (!permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials)
            || !permissions.HasPermission(ApplicationPermissions.PLM.ManageProductionOrders)
                && !permissions.HasPermission(ApplicationPermissions.PLM.ManageManufacturingFormulas)))
            return Fail("Bạn không có quyền sửa thành phần công thức khi copy lệnh.");
        if (command.SourceId == Guid.Empty || input.TotalQuantity <= 0 || input.NumOfBatches <= 0
            || input.StepOfProduct is null
            || !Enum.IsDefined(input.StepOfProduct.Value))
        {
            return Fail("Lệnh nguồn/lượng/số mẻ/công đoạn không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        await using var transaction = await db.BeginTransactionAsync(ct);
        try
        {
            await db.LockProductionOrderAsync(command.SourceId, company, ct);
            var link = await db.MfgOrderPOs.Where(x => x.MfgProductionOrderId == command.SourceId && x.IsActive && x.ProductionOrder.CompanyId == company
                && x.ProductionOrder.IsActive
                && x.Detail.IsActive
                && x.Detail.MerchandiseOrder.CompanyId == company
                && x.Detail.MerchandiseOrder.IsActive)
                .Include(x => x.ProductionOrder)
                .Include(x => x.Detail)
                .FirstOrDefaultAsync(ct);
            if (link is null)
            {
                return Fail("Không tìm thấy lệnh nguồn liên kết đơn hàng trong công ty.");
            }

            var source = link.ProductionOrder;
            if (source.Status == nameof(ManufacturingProductOrder.WaitingForStandardSample))
                return Fail("Lệnh nguồn đang chờ Sale xác nhận mẫu chuẩn.");
            if (link.Detail.ProductId != source.ProductId
                || !await db.Products.AsNoTracking().AnyAsync(x => x.ProductId == source.ProductId && x.CompanyId == company && x.IsActive, ct))
            {
                return Fail("Sản phẩm nguồn không hợp lệ.");
            }

            if (source.FormulaId.HasValue
                && !await db.Formulas.AsNoTracking().AnyAsync(x => x.FormulaId == source.FormulaId && x.ProductId == source.ProductId && x.CompanyId == company
                && x.IsActive, ct))
            {
                return Fail("VU nguồn không còn hợp lệ.");
            }

            var selected = await db.ProductionSelectVersions.AsNoTracking().Where(x => x.MfgProductionOrderId == command.SourceId && x.CompanyId == company && x.ValidTo == null)
                .OrderByDescending(x => x.ValidFrom)
                .ThenByDescending(x => x.ProductionSelectVersionId)
                .Select(x => (Guid? )x.ManufacturingFormulaId)
                .FirstOrDefaultAsync(ct);
            if (input.SelectedFormulaId.HasValue && input.SelectedFormulaId != selected)
            {
                return Fail("Công thức không phải lựa chọn hiện tại của lệnh nguồn.");
            }

            var items = new List<CreateProductionOrderFormulaItemRequest>();
            ManufacturingFormula? sourceFormula = null;
            ProductionOrderFormulaOptionDto? option = null;
            if (selected.HasValue)
            {
                await db.LockManufacturingFormulaAsync(selected.Value, company, ct);
                sourceFormula = await db.ManufacturingFormulas.AsNoTracking().FirstOrDefaultAsync(x => x.ManufacturingFormulaId == selected && x.CompanyId == company && x.IsActive, ct);
                if (sourceFormula is null)
                {
                    return Fail("VA nguồn không còn hợp lệ.");
                }

                option = new(
                    selected.Value,
                    sourceFormula.ExternalId,
                    ManufacturingFormulaOrigin.Production,
                    command.SourceId,
                    sourceFormula.CreatedDate,
                    false
                );
                var resolved = await inputs.ResolveAsync(source, option, input.Items, ct);
                if (!resolved.Success || resolved.Data is null)
                {
                    return Fail(resolved.Message ?? "Vật tư nguồn không hợp lệ.");
                }

                items = resolved.Data;
            }
            else if (input.Items is not null)
            {
                return Fail("Lệnh nguồn không có VA để sửa/copy vật tư.");
            }

            var request = new CreateProductionOrderInformRequest

            {

                MerchandiseOrderId = link.Detail.MerchandiseOrderId,

                MerchandiseOrderDetailId = link.Detail.MerchandiseOrderDetailId,

                ProductId = source.ProductId,

                ProductExternalIdSnapshot = source.ProductExternalIdSnapshot,

                ProductNameSnapshot = source.ProductNameSnapshot,

                CustomerExternalIdSnapshot = source.CustomerExternalIdSnapshot,

                CustomerNameSnapshot = source.CustomerNameSnapshot,

                FormulaCustomerSelect = source.FormulaId ?? Guid.Empty,

                FormulaCustomerExternalIdSelect = source.FormulaExternalIdSnapshot,

                ManufacturingFormulaIdIsSelect = selected,

                ManufacturingFormulaExternalIdIsSelect = sourceFormula?.ExternalId,
                // Owner confirmed the legacy form omits these fields: CustomerId=null, requested quantity/price=0.
                RequiredDate = source.RequiredDate,

                TotalQuantity = input.TotalQuantity,

                NumOfBatches = input.NumOfBatches,

                ExpectedDate = input.ExpectedDate,

                ManufacturingDate = input.ManufacturingDate,

                StepOfProduct = input.StepOfProduct,

                LabNote = input.LabNote ?? source.LabNote,

                Requirement = input.Requirement ?? source.Requirement,

                PlpuNote = option is null ? ManufacturingNoteRules.PreserveStoredPlpuAuto(input.PlpuNote ?? source.PlpuNote, source.PlpuNote) : await inputs.BuildPlpuNoteAsync(source, option, input.Items, items, input.PlpuNote ?? source.PlpuNote, ct),

                QcCheck = input.QcCheck ?? source.QcCheck,

                BagType = input.BagType ?? source.BagType,

                FormulaItems = items

            };
            var now = clock.Now;
            var copy = ProductionOrderInformFactory.Create(
                request,
                company,
                employee,
                now,
                await codes.GenerateMonthlyCodeAsync(company, DocumentPrefix.MFG.ToString(), ct)
            );
            await db.MfgProductionOrders.AddAsync(copy, ct);
            await db.MfgOrderPOs.AddAsync(new MfgOrderPO
            {
                MfgProductionOrderId = copy.MfgProductionOrderId,
                MerchandiseOrderDetailId = request.MerchandiseOrderDetailId,
                IsActive = true
            }, ct);
            ManufacturingFormula? formula = null;
            if (items.Count > 0)
            {
                formula = ProductionOrderFormulaFactory.Create(
                    request,
                    company,
                    employee,
                    now,
                    await codes.GenerateMonthlyCodeAsync(company, DocumentPrefix.VA.ToString(), ct)
                );
                await db.ManufacturingFormulas.AddAsync(formula, ct);
                await db.ProductionSelectVersions.AddAsync(ProductionOrderFormulaFactory.CreateSelection(copy, formula, now), ct);
                await db.SchedualMfgs.AddAsync(ProductionOrderFormulaFactory.CreateSchedule(copy, now), ct);
                var reservation = await reservations.SyncAsync(copy, items, now, ct);
                if (!reservation.Success)
                {
                    return Fail(reservation.Message ?? "Không thể giữ hàng cho lệnh copy.");
                }
            }

            await timeline.AddAsync(new EventLogCreateRequest
            {
                CompanyId = company,
                EmployeeId = employee,
                SourceType = "MfgProductionOrder",
                SourceId = copy.MfgProductionOrderId,
                SourceCode = copy.ExternalId,
                ParentSourceType = "MerchandiseOrder",
                ParentSourceId = request.MerchandiseOrderId,
                EventType = EventType.ManufacturingProductOrder,
                Status = copy.Status,
                CreatedDate = now,
                Note = $"Tạo lệnh sản xuất bởi {user.UserName} vào {now}"
            }, ct);
            // Legacy CreateInform does not see its Added link before SaveChanges; preserve sale status.
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<CreateProductionOrderInformResult>.Ok(new CreateProductionOrderInformResult(copy.MfgProductionOrderId, copy.ExternalId, formula?.ManufacturingFormulaId, formula?.ExternalId));
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            return Fail("Không thể copy lệnh sản xuất. Vui lòng tải lại dữ liệu.");
        }
    }

    private static OperationResult<CreateProductionOrderInformResult> Fail(string message) => OperationResult<CreateProductionOrderInformResult>.Fail(message);
}
