using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Helpers;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderFormulaSource;

internal sealed class GetProductionOrderFormulaSourceQueryHandler(
    IManufacturingDbContext db,
    IPLMReadDbContext displayDb,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IPLMFieldVisibilityService visibility
) : IRequestHandler<GetProductionOrderFormulaSourceQuery, OperationResult<ProductionOrderFormulaSourceDto>>
{
    public async Task<OperationResult<ProductionOrderFormulaSourceDto>> Handle(GetProductionOrderFormulaSourceQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null || !visibility.CanViewFormulaMaterials())
        {
            return Fail(error ?? "Bạn không có quyền xem vật tư công thức.");
        }

        var company = user.CompanyId!.Value;
        var order = await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MfgProductionOrderId == request.Id && x.CompanyId == company && x.IsActive, ct);
        if (order is null)
        {
            return Fail("Không tìm thấy lệnh sản xuất trong công ty.");
        }

        var source = (await ManufacturingFormulaOptionReader.ReadAsync(db, order, ct)).FirstOrDefault(x => x.FormulaId == request.FormulaId);
        var selected = await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.MfgProductionOrderId == request.Id && x.ManufacturingFormulaId == request.FormulaId
            && x.CompanyId == company
            && x.ValidTo == null, ct);
        if (source is null && !selected)
        {
            return Fail("Công thức không thuộc nguồn/lựa chọn của lệnh.");
        }

        var vu = source?.Origin is ManufacturingFormulaOrigin.FromVu or ManufacturingFormulaOrigin.Improvement;
        var canViewPrice = visibility.CanViewFormulaPrices();
        var data = vu ? await db.Formulas.AsNoTracking().Where(x => x.FormulaId == request.FormulaId && x.IsActive && x.CompanyId == company)
            .Select(x => new { x.ExternalId, Note = !string.IsNullOrWhiteSpace(x.Note) ? x.Note : x.Product.Requirement,
                CreatedDate = x.CreatedDate, Status = (string?)null })
            .FirstOrDefaultAsync(ct) : await db.ManufacturingFormulas.AsNoTracking().Where(x => x.ManufacturingFormulaId == request.FormulaId && x.IsActive && x.CompanyId == company)
            .Select(x => new { x.ExternalId, x.Note, CreatedDate = (DateTime?)x.CreatedDate, Status = (string?)x.Status })
            .FirstOrDefaultAsync(ct);
        if (data is null)
        {
            return Fail("Không tìm thấy công thức active trong công ty.");
        }

        var rows = vu ? await db.FormulaMaterials.AsNoTracking().Where(x => x.FormulaId == request.FormulaId && x.IsActive && x.Formula.CompanyId == company)
            .OrderBy(x => x.LineNo)
            .Select(x => new ProductionOrderFormulaSourceItemDto(
            x.FormulaMaterialId,
            x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure ? x.MaterialId ?? Guid.Empty : x.ProductId ?? Guid.Empty,
            x.itemType,
            x.CategoryId,
            x.LineNo,
            x.Quantity,
            canViewPrice ? x.UnitPrice : null,
            x.MaterialNameSnapshot,
            x.MaterialExternalIdSnapshot,
            x.Unit,
            null
        ))
            .ToListAsync(ct) : await db.ManufacturingFormulaMaterials.AsNoTracking().Where(x => x.ManufacturingFormulaId == request.FormulaId && x.IsActive
            && x.ManufacturingFormula.CompanyId == company)
            .OrderBy(x => x.LineNo)
            .Select(x => new ProductionOrderFormulaSourceItemDto(
            x.ManufacturingFormulaMaterialId,
            x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure ? x.MaterialId ?? Guid.Empty : x.ProductId ?? Guid.Empty,
            x.itemType,
            x.CategoryId,
            x.LineNo,
            x.Quantity,
            canViewPrice ? x.UnitPrice : null,
            x.MaterialNameSnapshot,
            x.MaterialExternalIdSnapshot,
            x.Unit,
            x.LotNo
        ))
            .ToListAsync(ct);
        var displays = await FormulaItemDisplayResolver.LoadCurrentDataAsync(
            displayDb,
            company,
            rows.Select(x => new FormulaItemDisplaySource(x.ItemId, x.ItemType, x.Name, x.Code)),
            ct
        );
        var items = rows.Select(x =>
        {
            var display = FormulaItemDisplayResolver.Resolve(new FormulaItemDisplaySource(x.ItemId, x.ItemType, x.Name, x.Code), displays);
            return x with

            {

                Name = display.Name,

                Code = display.ExternalId

            };
        })
            .ToList();
        items = await ManufacturingFormulaStockReader.FillAsync(db, company, items, ct,
            visibility.CanViewManufacturingDefectiveLots(),
            useGoodFifoDefault: order.Status == ManufacturingProductOrder.New.ToString());
        return OperationResult<ProductionOrderFormulaSourceDto>.Ok(new ProductionOrderFormulaSourceDto(request.FormulaId, data.ExternalId, data.Note, canViewPrice, items)
        {
            Origin = source?.Origin ?? ManufacturingFormulaOptionReader.ProductionOrigin(data.CreatedDate ?? order.CreatedDate),
            CreatedDate = data.CreatedDate,
            Status = data.Status
        });
    }

    private static OperationResult<ProductionOrderFormulaSourceDto> Fail(string message) => OperationResult<ProductionOrderFormulaSourceDto>.Fail(message);
}
