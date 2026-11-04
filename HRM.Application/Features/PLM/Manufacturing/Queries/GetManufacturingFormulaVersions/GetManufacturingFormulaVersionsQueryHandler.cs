using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaVersions;

internal sealed class GetManufacturingFormulaVersionsQueryHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IPLMFieldVisibilityService visibility
) : IRequestHandler<GetManufacturingFormulaVersionsQuery, OperationResult<PagedResult<ManufacturingFormulaVersionDto>>>
{
    public async Task<OperationResult<PagedResult<ManufacturingFormulaVersionDto>>> Handle(GetManufacturingFormulaVersionsQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null || !visibility.CanViewFormulaMaterials())
        {
            return Fail(error ?? "Không có quyền xem vật tư công thức.");
        }

        var skip = ((long)request.PageNumber - 1) * request.PageSize;
        if (request.FormulaId == Guid.Empty || request.PageNumber < 1 || request.PageSize is < 1 or > 100
            || skip > int.MaxValue)
        {
            return Fail("Công thức hoặc phân trang không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        // Versions have no CompanyId; scope every access through their formula parent.
        if (!await db.ManufacturingFormulas.AsNoTracking().AnyAsync(x => x.ManufacturingFormulaId == request.FormulaId && x.CompanyId == company && x.IsActive, ct))
        {
            return Fail("Không tìm thấy VA active trong công ty.");
        }

        var query = db.ManufacturingFormulaVersions.AsNoTracking().Where(x => x.ManufacturingFormulaId == request.FormulaId && x.ManufacturingFormula.CompanyId == company);
        var count = await query.CountAsync(ct);
        var headers = await query.OrderByDescending(x => x.VersionNo).ThenByDescending(x => x.ManufacturingFormulaVersionId)
            .Skip((int)skip)
            .Take(request.PageSize)
            .Select(x => new { x.ManufacturingFormulaVersionId, x.VersionNo, x.Status, x.EffectiveFrom, x.EffectiveTo, x.Note })
            .ToListAsync(ct);
        var ids = headers.Select(x => x.ManufacturingFormulaVersionId).ToArray();
        var canViewPrice = visibility.CanViewFormulaPrices();
        var items = await db.ManufacturingFormulaVersionItems.AsNoTracking().Where(x => ids.Contains(x.ManufacturingFormulaVersionId) && x.Version.ManufacturingFormula.CompanyId == company)
            .OrderBy(x => x.LineNo == 0 ? int.MaxValue : x.LineNo)
            .ThenBy(x => x.MaterialExternalIdSnapshot)
            .Select(x => new { x.ManufacturingFormulaVersionId, Item = new ManufacturingFormulaVersionItemDto(
            x.ManufacturingFormulaVersionItemId,
            x.itemType == ItemType.Material || x.itemType == ItemType.MaterialFailure ? x.MaterialId ?? Guid.Empty : x.ProductId ?? Guid.Empty,
            x.itemType,
            x.CategoryId,
            x.LineNo,
            x.Quantity,
            canViewPrice ? x.UnitPrice : null,
            canViewPrice ? x.TotalPrice : null,
            x.MaterialNameSnapshot,
            x.MaterialExternalIdSnapshot,
            x.Unit
        ) })
            .ToListAsync(ct);
        var lookup = items.ToLookup(x => x.ManufacturingFormulaVersionId, x => x.Item);
        var data = headers.Select(x => new ManufacturingFormulaVersionDto(
            x.ManufacturingFormulaVersionId,
            x.VersionNo,
            x.Status,
            x.EffectiveFrom,
            x.EffectiveTo,
            x.Note,
            lookup[x.ManufacturingFormulaVersionId].ToList()
        ))
            .ToList();
        return OperationResult<PagedResult<ManufacturingFormulaVersionDto>>.Ok(new PagedResult<ManufacturingFormulaVersionDto>(data, count, request.PageNumber, request.PageSize));
    }

    private static OperationResult<PagedResult<ManufacturingFormulaVersionDto>> Fail(string message) => OperationResult<PagedResult<ManufacturingFormulaVersionDto>>.Fail(message);
}
