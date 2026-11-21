using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Enums.Logs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.FillManufacturingColourNames;

internal sealed class FillManufacturingColourNamesCommandHandler(IManufacturingDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IDateTimeProvider clock, IEventLogWriter timeline)
    : IRequestHandler<FillManufacturingColourNamesCommand, OperationResult<FillManufacturingColourNamesResult>>
{
    public async Task<OperationResult<FillManufacturingColourNamesResult>> Handle(FillManufacturingColourNamesCommand request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders, true);
        if (error is not null || !user.IsInAnyRole(ApplicationRoleSets.PLM.ProductTechnicalEditors))
            return OperationResult<FillManufacturingColourNamesResult>.Fail(error ?? "Không có quyền sửa thông tin kỹ thuật sản phẩm.");

        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        var source = db.MfgProductionOrders.AsNoTracking()
            .Where(o => o.CompanyId == company && o.IsActive && o.ColorName == null);
        var products = db.Products.AsNoTracking().Where(p => p.CompanyId == company && p.IsActive
            && p.ColourName != null && p.ColourName.Trim() != "");
        var examined = 0; var matched = 0; var updated = 0; var conflicts = 0;
        var preview = new List<ManufacturingColourNameCandidate>();
        Guid? cursor = null;
        await using var transaction = request.DryRun ? null : await db.BeginTransactionAsync(ct);
        while (true)
        {
            var orders = source.Where(o => !cursor.HasValue || o.MfgProductionOrderId.CompareTo(cursor.Value) > 0);
            var page = await (from order in orders
                join product in products on order.ProductId equals product.ProductId into productGroup
                from product in productGroup.DefaultIfEmpty()
                orderby order.MfgProductionOrderId
                select new
                {
                    order.MfgProductionOrderId, order.ExternalId, order.ProductId, order.ProductNameSnapshot, order.Status,
                    ColourName = product == null ? null : product.ColourName
                }).Take(250).ToArrayAsync(ct);
            if (page.Length == 0) break;
            foreach (var order in page)
            {
                examined++;
                if (preview.Count < 100)
                    preview.Add(new(order.MfgProductionOrderId, order.ExternalId, order.ProductId, order.ProductNameSnapshot, order.ColourName));
                if (order.ColourName is not { } colour) continue;
                matched++;
                if (request.DryRun) continue;
                var now = clock.Now;
                // Recheck NULL và nguồn ngay lúc UPDATE, tránh ghi đè thay đổi sau khi đọc trang.
                var affected = await source.Where(o => o.MfgProductionOrderId == order.MfgProductionOrderId
                    && o.ProductId == order.ProductId
                    && products.Any(p => p.ProductId == o.ProductId && p.ColourName == colour))
                    .ExecuteUpdateAsync(set => set.SetProperty(o => o.ColorName, colour)
                        .SetProperty(o => o.UpdatedBy, employee).SetProperty(o => o.UpdatedDate, now), ct);
                if (affected == 0) { conflicts++; continue; }
                await timeline.AddAsync(new EventLogCreateRequest
                {
                    CompanyId = company, EmployeeId = employee, SourceType = "MfgProductionOrder",
                    SourceId = order.MfgProductionOrderId, SourceCode = order.ExternalId,
                    EventType = EventType.ManufacturingProductOrder, Status = order.Status, CreatedDate = now,
                    Note = $"Bổ sung snapshot màu từ Product.ColourName: NULL → {colour}."
                }, ct);
                updated++;
            }
            if (!request.DryRun) await db.SaveChangesAsync(ct);
            cursor = page[^1].MfgProductionOrderId;
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return OperationResult<FillManufacturingColourNamesResult>.Ok(new FillManufacturingColourNamesResult(request.DryRun, examined, matched, updated,
            conflicts, preview, examined > preview.Count));
    }
}
