using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaEditor;

internal sealed class GetManufacturingFormulaEditorQueryHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IPLMFieldVisibilityService visibility,
    IDateTimeProvider clock,
    ManufacturingFormulaReadService reader,
    ISender sender
) : IRequestHandler<GetManufacturingFormulaEditorQuery, OperationResult<ManufacturingFormulaEditorDto>>
{
    public async Task<OperationResult<ManufacturingFormulaEditorDto>> Handle(GetManufacturingFormulaEditorQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null || !visibility.CanViewFormulaMaterials())
        {
            return Fail(error ?? "Không có quyền xem vật tư công thức.");
        }

        if (request.OrderId == Guid.Empty || request.FormulaId == Guid.Empty)
        {
            return Fail("Reference không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var link = await db.MfgOrderPOs.AsNoTracking().Where(x => x.MfgProductionOrderId == request.OrderId && x.IsActive && x.ProductionOrder.CompanyId == company
            && x.ProductionOrder.IsActive
            && x.Detail.IsActive
            && x.Detail.MerchandiseOrder.CompanyId == company
            && x.Detail.MerchandiseOrder.IsActive)
            .Select(x => new { x.ProductionOrder.ProductId, x.ProductionOrder.FormulaId })
            .FirstOrDefaultAsync(ct);
        if (link is null)
        {
            return Fail("Không tìm thấy liên kết đơn hàng của MFG trong công ty.");
        }

        var order = await sender.Send(new GetProductionOrderQuery(request.OrderId), ct);
        if (!order.Success || order.Data is null)
        {
            return Fail(order.Message ?? "Không tìm thấy lệnh.");
        }

        var product = await db.Products.AsNoTracking().Where(x => x.ProductId == link.ProductId && x.CompanyId == company)
            .Select(x => new { x.Name, x.ColourCode, x.IsRecycle })
            .FirstOrDefaultAsync(ct);
        Guid sourceId;
        bool vu;
        string? code, name, note, status;
        DateTime created;
        bool selected = false, standard = false;
        Guid originId;
        FormulaSource originType;
        string? originCode;
        if (request.FormulaId.HasValue)
        {
            if (!await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.MfgProductionOrderId == request.OrderId && x.ManufacturingFormulaId == request.FormulaId
                && x.CompanyId == company, ct))
            {
                return Fail("VA không thuộc lịch sử MFG.");
            }

            sourceId = request.FormulaId.Value;
            vu = false;
        }
        else
        {
            var now = clock.Now;
            var candidate = await (
                from s in db.ProductStandardFormulas.AsNoTracking()join f in db.ManufacturingFormulas.AsNoTracking()on s.ManufacturingFormulaId equals f.ManufacturingFormulaId
                where s.CompanyId == company && f.CompanyId == company && s.ProductId == link.ProductId && f.IsActive
                && s.ValidFrom <= now
                && (s.ValidTo == null || s.ValidTo > now)orderby s.ValidFrom descending
                select new
                {
                    f.ManufacturingFormulaId,
                    f.SourceVUFormulaId
                }

            ).FirstOrDefaultAsync(ct);
            vu = candidate is null || link.FormulaId.HasValue && candidate.SourceVUFormulaId != link.FormulaId;
            if (vu && !link.FormulaId.HasValue)
            {
                return Fail("Lệnh không có nguồn VA chuẩn hoặc VU đã chọn.");
            }

            sourceId = vu ? link.FormulaId!.Value : candidate!.ManufacturingFormulaId;
        }

        if (vu)
        {
            var source = await db.Formulas.AsNoTracking().Where(x => x.FormulaId == sourceId && x.CompanyId == company && x.IsActive && x.ProductId == link.ProductId)
                .Select(x => new { x.ExternalId, x.Name, x.CreatedDate })
                .FirstOrDefaultAsync(ct);
            if (source is null)
            {
                return Fail("Không tìm thấy VU nguồn active của sản phẩm.");
            }

            code = source.ExternalId;
            name = source.Name;
            created = source.CreatedDate ?? DateTime.MinValue;
            note = null;
            status = null;
            originId = sourceId;
            originType = FormulaSource.FromVU;
            originCode = code;
        }
        else
        {
            var source = await db.ManufacturingFormulas.AsNoTracking().Where(x => x.ManufacturingFormulaId == sourceId && x.CompanyId == company && x.IsActive)
                .Select(x => new { x.ExternalId, x.Name, x.CreatedDate, x.Note, x.Status, x.SourceType, x.SourceManufacturingFormulaId, x.SourceVUFormulaId, x.SourceManufacturingExternalIdSnapshot, x.SourceVUExternalIdSnapshot })
                .FirstOrDefaultAsync(ct);
            if (source is null)
            {
                return Fail("Không tìm thấy VA active trong công ty.");
            }

            code = source.ExternalId;
            name = source.Name;
            created = source.CreatedDate;
            note = source.Note;
            status = source.Status;
            originId = request.FormulaId.HasValue ? source.SourceManufacturingFormulaId ?? source.SourceVUFormulaId ?? Guid.Empty : sourceId;
            originType = request.FormulaId.HasValue ? source.SourceType : FormulaSource.FromVA;
            originCode = request.FormulaId.HasValue ? source.SourceManufacturingExternalIdSnapshot ?? source.SourceVUExternalIdSnapshot : code;
            selected = await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.MfgProductionOrderId == request.OrderId
                && x.ManufacturingFormulaId == sourceId
                && x.ValidTo == null, ct);
            standard = await db.ProductStandardFormulas.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.ProductId == link.ProductId && x.ManufacturingFormulaId == sourceId
                && x.ValidTo == null, ct);
        }

        // Prefill legacy includes inactive source rows; View only active. New writes receive
        // the explicit chosen list, validate every reference, and never take costs from FE.
        var items = await reader.ReadAsync(
            company,
            sourceId,
            vu,
            includeInactive: !request.FormulaId.HasValue,
            latestProductPrices: request.FormulaId.HasValue,
            ct
        );
        var prices = visibility.CanViewFormulaPrices();
        return OperationResult<ManufacturingFormulaEditorDto>.Ok(new ManufacturingFormulaEditorDto(
            order.Data,
            request.FormulaId,
            request.FormulaId.HasValue ? code : null,
            name,
            status,
            originId,
            originType,
            originCode,
            created,
            note,
            !request.FormulaId.HasValue || selected,
            request.FormulaId.HasValue && standard,
            product?.Name,
            product?.ColourCode,
            product?.IsRecycle ?? false,
            prices,
            prices ? items.Sum(x => x.Quantity * (x.UnitPrice ?? 0)) : null,
            items
        ));
    }

    private static OperationResult<ManufacturingFormulaEditorDto> Fail(string message) => OperationResult<ManufacturingFormulaEditorDto>.Fail(message);
}
