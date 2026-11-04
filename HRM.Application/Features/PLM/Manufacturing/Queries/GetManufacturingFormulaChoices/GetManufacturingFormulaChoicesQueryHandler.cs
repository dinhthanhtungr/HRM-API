using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaChoices;

internal sealed class GetManufacturingFormulaChoicesQueryHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions,
    IDateTimeProvider clock
) : IRequestHandler<GetManufacturingFormulaChoicesQuery, OperationResult<PagedResult<ManufacturingFormulaChoiceDto>>>
{
    public async Task<OperationResult<PagedResult<ManufacturingFormulaChoiceDto>>> Handle(GetManufacturingFormulaChoicesQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null)
        {
            return Fail(error);
        }

        var r = request.Options;
        var skip = ((long)r.PageNumber - 1) * r.PageSize;
        if (r.PageNumber < 1 || r.PageSize is < 1 or > 100 || skip > int.MaxValue || r.FormulaId == Guid.Empty
            || r.OrderId == Guid.Empty
            || r.Keyword?.Length > 255)
        {
            return Fail("Bộ lọc không hợp lệ.");
        }

        var query = Build(db, user.CompanyId!.Value, clock.Now, r);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.IsSelected).ThenByDescending(x => x.IsStandard).ThenBy(x => x.CreatedDate)
            .ThenBy(x => x.FormulaId)
            .Skip((int)skip)
            .Take(r.PageSize)
            .ToListAsync(ct);
        return OperationResult<PagedResult<ManufacturingFormulaChoiceDto>>.Ok(new PagedResult<ManufacturingFormulaChoiceDto>(items, count, r.PageNumber, r.PageSize));
    }

    internal static IQueryable<ManufacturingFormulaChoiceDto> Build(
        IManufacturingDbContext db,
        Guid company,
        DateTime now,
        ManufacturingFormulaChoiceOptions options
    )
    {
        var orders = db.MfgProductionOrders.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive);
        var selections = db.ProductionSelectVersions.AsNoTracking().Where(x => x.CompanyId == company && orders.Any(o => o.MfgProductionOrderId == x.MfgProductionOrderId));
        var standards = db.ProductStandardFormulas.AsNoTracking().Where(x => x.CompanyId == company && x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo >= now));
        var formulas = db.ManufacturingFormulas.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive);
        if (options.FormulaId.HasValue)
        {
            formulas = formulas.Where(x => x.ManufacturingFormulaId == options.FormulaId);
        }

        if (options.OrderId.HasValue)
        {
            formulas = formulas.Where(x => selections.Any(s => s.ManufacturingFormulaId == x.ManufacturingFormulaId && s.MfgProductionOrderId == options.OrderId));
        }

        if (!string.IsNullOrWhiteSpace(options.Keyword))
        {
            var pattern = $"%{options.Keyword.Trim()}%";
            formulas = formulas.Where(x => selections.Any(s => s.ManufacturingFormulaId == x.ManufacturingFormulaId
                && orders.Any(o => o.MfgProductionOrderId == s.MfgProductionOrderId && o.Product.CompanyId == company
                && (EF.Functions.ILike(o.Product.ColourCode ?? "", pattern)
                || EF.Functions.ILike(o.Product.Name ?? "", pattern)))));
        }

        return formulas.Select(f => new ManufacturingFormulaChoiceDto
        {
            FormulaId = f.ManufacturingFormulaId,
            ExternalId = f.ExternalId,
            Status = f.Status,
            CreatedDate = f.CreatedDate,
            OrderExternalId = (
            from s in selections
            join o in orders on s.MfgProductionOrderId equals o.MfgProductionOrderId
            where s.ManufacturingFormulaId == f.ManufacturingFormulaId
            orderby s.ValidFrom descending, s.ProductionSelectVersionId descending
            select o.ExternalId).FirstOrDefault(),
            IsSelected = selections.Any(s => s.ManufacturingFormulaId == f.ManufacturingFormulaId && s.ValidTo == null),
            IsStandard = (
            from s in selections
            join o in orders on s.MfgProductionOrderId equals o.MfgProductionOrderId
            where s.ManufacturingFormulaId == f.ManufacturingFormulaId
            orderby s.ValidFrom descending, s.ProductionSelectVersionId descending
            select standards.Any(p => p.ProductId == o.ProductId && p.ManufacturingFormulaId == f.ManufacturingFormulaId
                && !standards.Any(later => later.ProductId == p.ProductId && later.ValidFrom > p.ValidFrom))).FirstOrDefault()
        });
    }

    private static OperationResult<PagedResult<ManufacturingFormulaChoiceDto>> Fail(string message) => OperationResult<PagedResult<ManufacturingFormulaChoiceDto>>.Fail(message);
}
