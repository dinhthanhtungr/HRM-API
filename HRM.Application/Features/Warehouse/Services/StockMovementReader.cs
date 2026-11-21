using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Services;

internal static class StockMovementReader
{
    public static readonly string[] BusinessTypes = ["InventoryCheck", "ToMixing", "FromMixing", "InternalTransfer", "Adjustment", "ExportToMixing", "Voucher", "Increase", "Decrease", "Legacy", "QcRawPass"];

    // Restrict this operational history to existing company-wide warehouse readers.
    public static bool CanRead(ICurrentUser user) => WarehouseVoucherAccessRules.CanReadAll(user);

    public static async Task<string> ResolveLegacyKeyAsync(IQueryable<WarehouseMovementRow> visible, string key, CancellationToken ct)
    {
        const string prefix = "LEGACY_LEDGER:";
        if (!key.StartsWith(prefix, StringComparison.Ordinal) || !long.TryParse(key[prefix.Length..], out var id) || id <= 0) return key;
        return await visible.Where(x => x.LedgerId == id).Select(x => x.MovementKey).FirstOrDefaultAsync(ct) ?? key;
    }

    public static IQueryable<WarehouseMovementRow> VisibleRows(
        IWarehouseReadDbContext db, ICurrentUser user, ICurrentUserPermissionService permissions)
    {
        var rows = db.ReadMovementHistory(user.CompanyId!.Value).Where(x => x.CompanyId == user.CompanyId);
        if (!permissions.HasPermission(ApplicationPermissions.Warehouse.ViewMixingStock))
        {
            // Hide the whole group, rather than returning half of a transfer or leaking CT totals.
            var restricted = rows.Where(x => x.Scope == "CT").Select(x => x.MovementKey);
            rows = rows.Where(x => !restricted.Contains(x.MovementKey));
        }
        return rows;
    }

    public static string Category(WarehouseMovementRow row, StockMovementContext? context = null) => row.MovementType == "QC_WAIT_TO_RAW_PASS" ? "QcRawPass"
        : row.AppSource == "Warehouse.InventoryCheck" ? "InventoryCheck"
        : row.MovementType == "NORMAL_TO_CT" ? "ToMixing"
        : row.MovementType == "CT_TO_NORMAL" ? "FromMixing"
        : row.MovementType is "NORMAL_INTERNAL" or "CT_INTERNAL" or "INTERNAL_MIXED" or "VERIFIED_LEGACY_TRANSFER" ? "InternalTransfer"
        : row.AppSource == "Warehouse.AdjustUI" ? "Adjustment"
        : row.MovementType == "NORMAL_OUT" && row.Scope == "NORMAL" && row.DeltaKg < 0
            && row.RequestCode != null && context?.MixingExportRequests.Contains(row.RequestCode) == true ? "ExportToMixing"
        : row.VoucherId.HasValue ? "Voucher"
        : !row.IsCanonicalMovement ? "Legacy"
        : row.DeltaKg > 0 ? "Increase" : "Decrease";

    public static WarehouseVoucherBusinessTypeDto Label(string code) => code switch
    {
        "InventoryCheck" => new(code, "Kiểm kê kho", "Stocktake", "primary"),
        "QcRawPass" => new(code, "Chờ QC → NVL Đạt", "Awaiting QC → Accepted raw material", "success"),
        "ToMixing" => new(code, "Chuyển sang kho cân trộn", "Transfer to mixing", "primary"),
        "ExportToMixing" => new(code, "Xuất kho cho cân trộn", "Issue for mixing", "warning"),
        "FromMixing" => new(code, "Chuyển từ kho cân trộn", "Transfer from mixing", "primary"),
        "InternalTransfer" => new(code, "Chuyển nội bộ / đổi loại tồn", "Internal transfer / stock reclassification", "primary"),
        "Adjustment" => new(code, "Điều chỉnh tồn kho", "Stock adjustment", "warning"),
        "Voucher" => new(code, "Nhập / xuất theo phiếu kho", "Warehouse voucher movement", "muted"),
        "Increase" => new(code, "Tăng tồn kho", "Stock increase", "success"),
        "Decrease" => new(code, "Giảm tồn kho", "Stock decrease", "warning"),
        _ => new("Legacy", "Biến động cũ chưa ghép nhóm", "Ungrouped legacy movement", "muted")
    };

    public static StockMovementDto Summarize(IReadOnlyList<WarehouseMovementRow> rows, StockMovementContext? context = null)
    {
        var first = rows[0];
        var category = rows.Select(row => Category(row, context)).OrderBy(x => Array.IndexOf(BusinessTypes, x)).First();
        return new(first.MovementKey, rows.Min(x => x.MovementAt), first.IsCanonicalMovement,
            Label(category), rows.Count, rows.Where(x => x.DeltaKg > 0).Sum(x => x.DeltaKg),
            -rows.Where(x => x.DeltaKg < 0).Sum(x => x.DeltaKg), rows.Sum(x => x.DeltaKg),
            rows.Select(x => x.ProductCode).OfType<string>().Distinct().Order().ToArray(),
            rows.Where(x => category == "QcRawPass" || x.DeltaKg < 0).Select(x => x.ShelfStockCode).Distinct().Order().ToArray(),
            rows.Where(x => category == "QcRawPass" || x.DeltaKg > 0).Select(x => x.ShelfStockCode).Distinct().Order().ToArray())
        {
            Goods = rows.Select(x => new StockMovementGoodsDto(x.ProductCode, x.LotNumber, x.StockType))
                .Distinct().OrderBy(x => x.ProductCode).ThenBy(x => x.LotNumber).ThenBy(x => x.StockType).ToArray(),
            // Before/after belong to an individual stock row, never sum them across transfer legs.
            BeforeKg = rows.Count == 1 ? first.BeforeKg : null,
            AfterKg = rows.Count == 1 ? first.AfterKg : null,
            CreatedByNames = rows.Select(x => x.CreatedBy.HasValue ? context?.Employees.GetValueOrDefault(x.CreatedBy.Value) : null)
                .OfType<string>().Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Order().ToArray(),
            ReferenceCodes = rows.Select(x => x.VoucherId.HasValue ? context?.Vouchers.GetValueOrDefault(x.VoucherId.Value) ?? x.RequestCode : x.RequestCode)
                .OfType<string>().Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Order().ToArray()
        };
    }
}
