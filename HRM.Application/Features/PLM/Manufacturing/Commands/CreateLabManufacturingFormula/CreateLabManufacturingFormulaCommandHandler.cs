using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Manufacturing.Services.Notifications;
using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.CreateLabManufacturingFormula;

internal sealed class CreateLabManufacturingFormulaCommandHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider clock,
    IExternalIdService codes,
    IEventLogWriter timeline,
    ManufacturingFormulaInputResolver resolver,
    ManufacturingWorkflowNotifier notifier
) : IRequestHandler<CreateLabManufacturingFormulaCommand, OperationResult<ProductionOrderFormulaCreatedDto>>
{
    public async Task<OperationResult<ProductionOrderFormulaCreatedDto>> Handle(CreateLabManufacturingFormulaCommand command, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ManageManufacturingFormulas, true);
        if (error is not null)
        {
            return Fail(error);
        }

        var r = command.Request;
        if (command.Id == Guid.Empty || r.SourceFormulaId == Guid.Empty
            || r.SourceType is not (FormulaSource.FromVA or FormulaSource.FromVU)
            || r.Items is null
            || r.Items.Count is 0 or > 1000
            || r.Items.Any(x => x is null || x.Quantity < 0 || x.Quantity > 1m)
            || r.Items.Sum(x => x.Quantity) != 1m)
        {
            return Fail("Nguồn và tổng tỷ lệ công thức không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        await using var tx = await db.BeginTransactionAsync(ct);
        try
        {
            var productId = await db.MfgProductionOrders.AsNoTracking().Where(x => x.MfgProductionOrderId == command.Id && x.CompanyId == company && x.IsActive)
                .Select(x => (Guid? )x.ProductId)
                .FirstOrDefaultAsync(ct);
            if (productId is null)
            {
                return Fail("Không tìm thấy lệnh sản xuất trong công ty.");
            }

            await db.LockManufacturingProductAsync(productId.Value, company, ct);
            await db.LockProductionOrderAsync(command.Id, company, ct);
            var link = await db.MfgOrderPOs.Where(x => x.MfgProductionOrderId == command.Id && x.IsActive && x.ProductionOrder.CompanyId == company
                && x.ProductionOrder.IsActive
                && x.Detail.IsActive
                && x.Detail.MerchandiseOrder.CompanyId == company
                && x.Detail.MerchandiseOrder.IsActive)
                .Include(x => x.ProductionOrder)
                .Include(x => x.Detail)
                .FirstOrDefaultAsync(ct);
            if (link is null || link.ProductionOrder.ProductId != productId)
            {
                return Fail("Liên kết đơn hàng không hợp lệ.");
            }

            var order = link.ProductionOrder;
            error = ManufacturingAccess.ValidateInformState(user, order.Status);
            if (error is not null) return Fail(error);
            error = ManufacturingAccess.ValidateFormulaEditState(order.Status);
            if (error is not null) return Fail(error);
            if (order.Status == nameof(ManufacturingProductOrder.WaitingForStandardSample))
                return OperationResult<ProductionOrderFormulaCreatedDto>.Fail("Lệnh đang chờ Sale xác nhận mẫu chuẩn.");
            var fromVu = r.SourceType == FormulaSource.FromVU;
            var source = (await ManufacturingFormulaOptionReader.ReadAsync(db, order, ct)).FirstOrDefault(x => x.FormulaId == r.SourceFormulaId
                && (x.Origin is ManufacturingFormulaOrigin.FromVu or ManufacturingFormulaOrigin.Improvement) == fromVu);
            ManufacturingFormula? sourceVa = null;
            if (!fromVu)
            {
                await db.LockManufacturingFormulaAsync(r.SourceFormulaId, company, ct);
                sourceVa = await db.ManufacturingFormulas.AsNoTracking().FirstOrDefaultAsync(x => x.ManufacturingFormulaId == r.SourceFormulaId && x.CompanyId == company && x.IsActive, ct);
                var own = await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.MfgProductionOrderId == command.Id && x.CompanyId == company
                    && x.ManufacturingFormulaId == r.SourceFormulaId, ct);
                // Prefill also permits a standard whose scheduled closing date is still in the future.
                var effectiveStandard = sourceVa is not null && (!order.FormulaId.HasValue || order.FormulaId == sourceVa.SourceVUFormulaId)
                    && await db.ProductStandardFormulas.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.ProductId == order.ProductId
                    && x.ManufacturingFormulaId == r.SourceFormulaId
                    && x.ValidFrom <= clock.Now
                    && (x.ValidTo == null || x.ValidTo > clock.Now), ct);
                if (source is null && (own || effectiveStandard) && sourceVa is not null)
                {
                    source = new(
                        r.SourceFormulaId,
                        sourceVa.ExternalId,
                        ManufacturingFormulaOrigin.Production,
                        command.Id,
                        sourceVa.CreatedDate,
                        true
                    );
                }
            }

            if (source is null)
            {
                return Fail("Nguồn công thức không thuộc lựa chọn/lịch sử của lệnh.");
            }

            var resolved = await resolver.ResolveAsync(
                order,
                source,
                r.Items,
                ct,
                allowZeroQuantity: true,
                includeInactiveSourceItems: true,
                refreshExistingPrices: true,
                refreshProductPrices: false
            );
            if (!resolved.Success || resolved.Data is null)
            {
                return Fail(resolved.Message ?? "Vật tư không hợp lệ.");
            } // Legacy names are a per-MFG F series, independent of the global VA code.

            var names = await (
                from v in db.ProductionSelectVersions.AsNoTracking()join f in db.ManufacturingFormulas.AsNoTracking()on v.ManufacturingFormulaId equals f.ManufacturingFormulaId
                where v.CompanyId == company && f.CompanyId == company && v.MfgProductionOrderId == command.Id
                && f.Name.StartsWith("F")select f.Name).ToListAsync(ct);
            var next = names.Select(x => int.TryParse(x.AsSpan(1), out var number) ? number : 0).DefaultIfEmpty(0).Max() + 1;
            var now = clock.Now;
            var request = new CreateProductionOrderInformRequest

            {

                FormulaCustomerSelect = fromVu ? r.SourceFormulaId : sourceVa?.SourceVUFormulaId ?? Guid.Empty,

                FormulaCustomerExternalIdSelect = fromVu ? source.ExternalId : sourceVa?.SourceVUExternalIdSnapshot,

                ManufacturingFormulaIdIsSelect = fromVu ? null : r.SourceFormulaId,

                ManufacturingFormulaExternalIdIsSelect = fromVu ? null : source.ExternalId,

                FormulaItems = resolved.Data

            };
            var formula = ProductionOrderFormulaFactory.Create(
                request,
                company,
                employee,
                now,
                await codes.GenerateMonthlyCodeAsync(company, DocumentPrefix.VA.ToString(), ct)
            );
            formula.Name = $"F{next:D3}";
            formula.SourceType = r.SourceType;
            formula.Note = r.Note;
            formula.UpdatedBy = employee;
            formula.UpdatedDate = now;
            // Inform's factory intentionally drops zero rows. Lab create retains every active
            // submitted row, including zero, as the old Lab writer did.
            formula.ManufacturingFormulaMaterials.Clear();
            ManufacturingFormulaItemWriter.Replace(formula, r.Items, resolved.Data, preserveLabUpsertBehavior: true);
            await db.ManufacturingFormulas.AddAsync(formula, ct);
            var open = await db.ProductionSelectVersions.Where(x => x.CompanyId == company && x.MfgProductionOrderId == command.Id && x.ValidTo == null)
                .ToListAsync(ct);
            foreach (var old in open)
            {
                old.ValidTo = now;
                old.ClosedBy = employee;
            }

            ProductStandardFormula? newStandard = null;
            if (r.IsStandard)
            {
                var standards = await db.ProductStandardFormulas.Where(x => x.CompanyId == company && x.ProductId == order.ProductId && x.ValidTo == null)
                    .ToListAsync(ct);
                foreach (var old in standards)
                {
                    old.ValidTo = now;
                    old.ClosedBy = employee;
                }

                newStandard = new ProductStandardFormula

                {

                    ProductStandardFormulaId = Guid.CreateVersion7(),

                    CompanyId = company,

                    ProductId = order.ProductId,

                    ManufacturingFormulaId = formula.ManufacturingFormulaId,

                    ValidFrom = now,

                    CreatedBy = employee,

                    Note = r.Note

                };
            }

            // Release existing open selections/standards before inserting under partial unique indexes.
            await db.SaveChangesAsync(ct);
            if (newStandard is not null)
            {
                await db.ProductStandardFormulas.AddAsync(newStandard, ct);
            }

            var selection = ProductionOrderFormulaFactory.CreateSelection(order, formula, now);
            selection.CreatedBy = employee;
            await db.ProductionSelectVersions.AddAsync(selection, ct);
            if (string.Equals(
                order.Status,
                ManufacturingProductOrder.FormulaRequested.ToString(),
                StringComparison.OrdinalIgnoreCase
            ))
            {
                order.Status = ManufacturingProductOrder.FormulaSuccess.ToString();
            }

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
                Note = $"Cập nhật bởi hệ thống vào {now} bởi {user.UserName}"
            }, ct);
            var sale = await db.MerchandiseOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MerchandiseOrderId == link.Detail.MerchandiseOrderId && x.CompanyId == company && x.IsActive, ct);
            if (sale is not null)
            {
                await notifier.PriceWarningAsync(order, formula, sale, link.Detail.UnitPriceAgreed, ct);
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return OperationResult<ProductionOrderFormulaCreatedDto>.Ok(new ProductionOrderFormulaCreatedDto(formula.ManufacturingFormulaId, formula.ExternalId, order.Status, true));
        }
        catch (Exception ex)when (ex is not OperationCanceledException)
        {
            await tx.RollbackAsync(ct);
            return Fail("Không thể tạo công thức sản xuất. Vui lòng tải lại dữ liệu.");
        }
    }

    private static OperationResult<ProductionOrderFormulaCreatedDto> Fail(string message) => OperationResult<ProductionOrderFormulaCreatedDto>.Fail(message);
}
