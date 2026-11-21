using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Application.Features.Warehouse.Queries.GetStockMovements;

internal static class StockMovementListQuery
{
    internal static IQueryable<IGrouping<string, WarehouseMovementRow>> Groups(
        IQueryable<WarehouseMovementRow> companyRows, bool canViewMixing)
    {
        var groups = companyRows.GroupBy(x => x.MovementKey);
        // HAVING checks complete groups once; a company-wide anti-subquery would repeat
        // the legacy normalization and canonical view for every list phase.
        return canViewMixing ? groups : groups.Where(g => !g.Any(x => x.Scope == "CT"));
    }

    internal static IQueryable<WarehouseMovementRow> PageRows(
        IQueryable<WarehouseMovementRow> companyRows, string[] keys, bool canViewMixing)
    {
        var rows = companyRows.Where(x => keys.Contains(x.MovementKey));
        if (!canViewMixing)
        {
            // Recheck the WHOLE selected group if it changed after paging. Restrict this
            // check to page keys too, rather than normalizing all company history again.
            var restricted = rows.Where(x => x.Scope == "CT").Select(x => x.MovementKey);
            rows = rows.Where(x => !restricted.Contains(x.MovementKey));
        }
        // MovementAt in the view is MIN(CreatedAt) over the movement. Every row of the
        // selected groups is loaded, so Summarize computes exactly the same minimum.
        // Avoid selecting the window column: PostgreSQL can then prune that window
        // and push the page-key predicate below it. Keep all audit/summary fields.
        return rows.Select(x => new WarehouseMovementRow
        {
            CompanyId = x.CompanyId, MovementKey = x.MovementKey, MovementId = x.MovementId,
            IsCanonicalMovement = x.IsCanonicalMovement, MovementType = x.MovementType,
            MovementAt = x.CreatedAt, CreatedAt = x.CreatedAt, LedgerId = x.LedgerId,
            Scope = x.Scope, SlotId = x.SlotId, ShelfStockCode = x.ShelfStockCode,
            ShelfStockId = x.ShelfStockId, ProductCode = x.ProductCode, LotNumber = x.LotNumber,
            StockType = x.StockType, UnitName = x.UnitName, DeltaKg = x.DeltaKg,
            BeforeKg = x.BeforeKg, AfterKg = x.AfterKg, CreatedBy = x.CreatedBy,
            RequestCode = x.RequestCode, AppSource = x.AppSource, VoucherId = x.VoucherId,
            VoucherDetailId = x.VoucherDetailId
        });
    }
}
