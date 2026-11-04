using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.Shared.Authorization;
using HRM.Domain.Enums.Formulas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulas;

internal sealed class GetManufacturingFormulasQueryHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IPLMFieldVisibilityService visibility,
    ManufacturingFormulaReadService reader
) : IRequestHandler<GetManufacturingFormulasQuery, OperationResult<PagedResult<ManufacturingFormulaSummaryDto>>>
{
    public async Task<OperationResult<PagedResult<ManufacturingFormulaSummaryDto>>> Handle(GetManufacturingFormulasQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null || !visibility.CanViewFormulaMaterials())
        {
            return Fail(error ?? "Không có quyền xem vật tư công thức.");
        }

        var r = request.Options;
        var skip = ((long)r.PageNumber - 1) * r.PageSize;
        if (r.PageNumber < 1 || r.PageSize is < 1 or > 100 || skip > int.MaxValue || r.ProductId == Guid.Empty
            || r.From > r.To
            || r.Source is not (FormulaSource.FromVA or FormulaSource.FromVU or FormulaSource.Both))
        {
            return Fail("Bộ lọc công thức không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var query = ManufacturingFormulaHeaderQuery.Build(db, company, r, request.Compare);
        var count = await query.CountAsync(ct);
        var headers = await query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.Id).Skip((int)skip).Take(r.PageSize)
            .ToListAsync(ct);
        var rows = await reader.ReadBatchAsync(
            company,
            headers.Where(x => x.Source == FormulaSource.FromVA).Select(x => x.Id).Distinct().ToArray(),
            headers.Where(x => x.Source == FormulaSource.FromVU).Select(x => x.Id).Distinct().ToArray(),
            false,
            false,
            false,
            ct,
            includePrices: !request.Compare
        );
        var prices = !request.Compare && visibility.CanViewFormulaPrices();
        var data = headers.Select(x =>
        {
            var items = rows.GetValueOrDefault((x.Id, x.Source == FormulaSource.FromVU)) ?? [];
            return new ManufacturingFormulaSummaryDto(
                x.Id,
                x.Source,
                x.ProductId,
                x.OrderId,
                x.ExternalId,
                x.Name,
                x.ColourCode,
                x.CreatedDate,
                prices ? items.Sum(i => i.Quantity * (i.UnitPrice ?? 0)) : null,
                items
            );
        })
            .ToList();
        return OperationResult<PagedResult<ManufacturingFormulaSummaryDto>>.Ok(new PagedResult<ManufacturingFormulaSummaryDto>(data, count, r.PageNumber, r.PageSize));
    }

    private static OperationResult<PagedResult<ManufacturingFormulaSummaryDto>> Fail(string message) => OperationResult<PagedResult<ManufacturingFormulaSummaryDto>>.Fail(message);
}
