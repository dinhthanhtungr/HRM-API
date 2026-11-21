using HRM.Application.Abstractions.Persistence.PLM.ProductionOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.MaterialProcessing.Services;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Queries.GetStockAvailable;
using HRM.Domain.Enums.WareHouses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.MaterialEquivalences;

internal sealed class GetMaterialDemandsQueryHandler(IProductionOrderDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ISender sender) : IRequestHandler<GetMaterialDemandsQuery, PagedResult<MaterialDemandDto>?>
{
    public async Task<PagedResult<MaterialDemandDto>?> Handle(GetMaterialDemandsQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !ApplicationRoleSets.DevAndQA.MaterialPlanningViewers.Any(user.IsInRole)
            || user.CompanyId is not Guid company || company == Guid.Empty
            || !permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials)) return null;
        var orders = db.MfgProductionOrders.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive
            && x.Product.CompanyId == company && x.Product.IsActive && MaterialDemandRules.ActiveStatuses.Contains(x.Status));
        var reservations = db.WarehouseTempStocks.AsNoTracking().Where(x => x.CompanyId == company && x.ReserveStatus == "Open"
            && x.Code.Trim() != "" && (x.QtyRequest ?? 0) > (x.QtyUsed ?? 0));
        var lines = await (from reserve in reservations
            join order in orders on reserve.VaCode.Trim().ToUpper() equals order.ExternalId.Trim().ToUpper()
            select new { Code = reserve.Code.Trim().ToUpper(), order.MfgProductionOrderId, order.ExternalId, order.Status, order.RequiredDate,
                RemainingKg = (reserve.QtyRequest ?? 0) - (reserve.QtyUsed ?? 0),
                IsInternal = order.Product.SampleRequests.Any(s => s.CompanyId == company && s.IsActive && s.RequestType == "SemiFinished") })
            .ToListAsync(ct);
        var codes = lines.Select(x => x.Code).Distinct().ToArray();
        var materials = await db.Materials.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive == true && x.ExternalId != null
            && codes.Contains(x.ExternalId.Trim().ToUpper())).Select(x => new { Code = x.ExternalId!.Trim().ToUpper(), Name = x.Name ?? "" }).ToListAsync(ct);
        var products = await db.Products.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && x.ColourCode != null
            && codes.Contains(x.ColourCode.Trim().ToUpper())).Select(x => new { Code = x.ColourCode!.Trim().ToUpper(), Name = x.Name ?? "" }).ToListAsync(ct);
        var names = materials.Concat(products).GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.First().Name);
        var materialCodes = materials.Select(x => x.Code).ToHashSet();
        var keyword = request.NormalizedKeyword;
        var filtered = lines.GroupBy(x => x.Code).Where(g => keyword is null || g.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || names.GetValueOrDefault(g.Key, "").Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || g.Any(x => x.ExternalId.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToList();
        var stockRows = new List<StockAvailableDto>();
        var canViewDefective = permissions.HasPermission(ApplicationPermissions.PLM.ViewManufacturingDefectiveLots);
        foreach (var batch in filtered.Select(x => x.Key).Chunk(20))
        {
            var stock = await sender.Send(new GetStockAvailableQuery { ExactCodes = batch, PageSize = 100,
                StockTypes = canViewDefective ? [StockType.RawMaterial, StockType.FinishedGood, StockType.DefectiveRawMaterial, StockType.DefectiveFinishedGood]
                    : [StockType.RawMaterial, StockType.FinishedGood] }, ct);
            if (!stock.Success || stock.Data is null) throw new InvalidOperationException("Cannot load warehouse stock for material demand.");
            stockRows.AddRange(stock.Data.Items);
        }
        var rootCodes = filtered.Select(x => MaterialProcessingRules.BaseCode(x.Key)).Distinct().ToArray();
        var roots = await db.Materials.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive == true && x.Category.CompanyId == company
            && x.Category.IsActive == true && x.Category.Types == "NVL" && x.Unit != null && x.Unit.Trim().ToLower() == "kg"
            && x.ExternalId != null && rootCodes.Contains(x.ExternalId.Trim().ToUpper()))
            .Select(x => new EquivalentMaterialDto(x.MaterialId, x.ExternalId!, x.Name ?? "", x.CategoryId, x.Unit)).ToListAsync(ct);
        var result = new List<MaterialDemandDto>();
        foreach (var group in filtered)
        {
            var name = names.GetValueOrDefault(group.Key, group.Key);
            var process = group.Key.EndsWith("-N") || group.Key.EndsWith("_N") ? "N"
                : group.Key.EndsWith("-B") || group.Key.EndsWith("_B") ? "B" : MaterialEquivalenceRules.ProcessingType(name);
            if (!string.IsNullOrEmpty(request.ProcessingType) && request.ProcessingType != process) continue;
            var normal = materialCodes.Contains(group.Key) ? StockType.RawMaterial : StockType.FinishedGood;
            var defective = materialCodes.Contains(group.Key) ? StockType.DefectiveRawMaterial : StockType.DefectiveFinishedGood;
            var stocks = stockRows.Where(x => x.Code.Trim().Equals(group.Key, StringComparison.OrdinalIgnoreCase)).ToArray();
            var good = stocks.Where(x => x.StockType == normal).ToArray();
            var available = good.Sum(x => x.AvailableKg);
            if (request.OnlyAvailableLeZero && available > 0 || request.AvailableMax.HasValue && available > request.AvailableMax.Value) continue;
            var remaining = group.Sum(x => x.RemainingKg);
            var root = roots.Where(x => x.Code.Trim().Equals(MaterialProcessingRules.BaseCode(group.Key), StringComparison.OrdinalIgnoreCase)).ToArray();
            var source = process is "B" or "N" && root.Length == 1 && MaterialProcessingRules.IsRootCode(root[0].Code) ? root[0] : null;
            result.Add(new(group.Key, name, process, remaining, good.Sum(x => x.TotalOnHandKg), available,
                MaterialDemandRules.Shortage(remaining, good.Sum(x => x.TotalOnHandKg), good.Sum(x => x.ReservedOpenAllKg)),
                canViewDefective ? stocks.Where(x => x.StockType == defective).Sum(x => x.TotalOnHandKg) : null, source,
                group.GroupBy(x => x.MfgProductionOrderId).Select(g => new MaterialDemandOrderDto(g.Key, g.First().ExternalId,
                    g.First().Status, g.First().RequiredDate, g.Sum(x => x.RemainingKg), g.First().IsInternal)).OrderBy(x => x.RequiredDate).ToArray()));
        }
        return new(result.OrderByDescending(x => x.ShortageKg).ThenBy(x => x.Orders.Min(o => o.RequiredDate)).ThenBy(x => x.Code)
            .Skip((request.NormalizedPageNumber - 1) * request.NormalizedPageSize).Take(request.NormalizedPageSize).ToArray(),
            result.Count, request.NormalizedPageNumber, request.NormalizedPageSize);
    }
}
