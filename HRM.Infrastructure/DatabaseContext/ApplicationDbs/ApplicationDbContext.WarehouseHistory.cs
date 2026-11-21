using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    // Read-only legacy normalization. xmin proves both entries were written in the same
    // transaction; exact trigger markers, adjacent IDs and all stock dimensions fail closed.
    // No persisted ledger IDs, MovementIds, quantities or schema objects are changed.
    public IQueryable<WarehouseMovementRow> ReadMovementHistory(Guid companyId)
        => WarehouseMovementRows.FromSqlInterpolated($"""
            WITH qc_pairs AS MATERIALIZED (
                SELECT a."ledgerId" AS out_id
                FROM "Warehouse"."WarehouseShelfLedger" a
                JOIN "Warehouse"."WarehouseShelfLedger" b ON b."ledgerId" = a."ledgerId" + 1
                WHERE a."companyId" = {companyId} AND b."companyId" = a."companyId"
                  AND a.xmin = b.xmin AND a."movementId" IS NULL AND b."movementId" IS NULL
                  AND a.reason = 'StockType changed OUT: 6 -> 3' AND b.reason = 'StockType changed IN: 6 -> 3'
                  AND a."stockType" = 6 AND b."stockType" = 3
                  AND a."slotId" = b."slotId" AND a."productCode" = b."productCode"
                  AND a."lotNumber" IS NOT NULL AND a."lotNumber" <> '' AND a."lotNumber" = b."lotNumber"
                  AND a."ExpiryDate" IS NOT DISTINCT FROM b."ExpiryDate"
                  AND a."UnitName" IS NOT DISTINCT FROM b."UnitName"
                  AND a."shelfStockId" IS NOT DISTINCT FROM b."shelfStockId"
                  AND a."createdBy" IS NOT NULL AND a."createdBy" = b."createdBy"
                  AND a."createdAt" = b."createdAt"
                  AND a."voucherId" IS NULL AND b."voucherId" IS NULL
                  AND a."voucherDetailId" IS NULL AND b."voucherDetailId" IS NULL
                  AND a."requestCode" IS NOT DISTINCT FROM b."requestCode"
                  AND a."appSource" IS NOT DISTINCT FROM b."appSource"
                  AND a."deltaKg" < 0 AND b."deltaKg" = -a."deltaKg"
                  AND a."beforeKg" = -a."deltaKg" AND a."afterKg" = 0
                  AND b."beforeKg" = 0 AND b."afterKg" = b."deltaKg"
            ), qc_keys AS (
                SELECT out_id AS ledger_id, out_id FROM qc_pairs
                UNION ALL
                SELECT out_id + 1 AS ledger_id, out_id FROM qc_pairs
                UNION ALL
                SELECT "ledgerId", "ledgerId" FROM "Warehouse"."WarehouseShelfLedger"
                WHERE "companyId" = {companyId} AND "movementId" IS NULL
                  AND reason = 'StockType changed: 6 -> 3' AND "stockType" = 3
                  AND "deltaKg" = 0 AND "beforeKg" = "afterKg" AND "afterKg" > 0
                  AND "voucherId" IS NULL AND "voucherDetailId" IS NULL
            ), transfer_pairs AS MATERIALIZED (
                SELECT a."ledgerId" AS out_id
                FROM "Warehouse"."WarehouseShelfLedger" a
                JOIN "Warehouse"."WarehouseShelfLedger" b ON b."ledgerId" = a."ledgerId" + 1
                WHERE a."companyId" = {companyId} AND b."companyId" = a."companyId"
                  AND a.xmin = b.xmin AND a."movementId" IS NULL AND b."movementId" IS NULL
                  AND a."appSource" = 'Warehouse.AdjustUI' AND b."appSource" = a."appSource"
                  AND a."requestCode" ~ '^WMV[.][0-9]+$' AND a."requestCode" = b."requestCode"
                  AND a."slotId" <> b."slotId" AND a."productCode" = b."productCode"
                  AND a."lotNumber" IS NOT NULL AND a."lotNumber" <> '' AND a."lotNumber" = b."lotNumber"
                  AND a."stockType" = b."stockType"
                  AND (a."ExpiryDate" IS NOT DISTINCT FROM b."ExpiryDate" OR a."ExpiryDate" IS NULL OR b."ExpiryDate" IS NULL)
                  AND a."UnitName" IS NOT DISTINCT FROM b."UnitName"
                  AND a."createdBy" IS NOT NULL AND a."createdBy" = b."createdBy"
                  AND a."createdAt" = b."createdAt" AND a.reason IS NULL AND b.reason IS NULL
                  AND a."voucherId" IS NULL AND b."voucherId" IS NULL
                  AND a."voucherDetailId" IS NULL AND b."voucherDetailId" IS NULL
                  AND a."deltaKg" < 0 AND b."deltaKg" = -a."deltaKg"
                  AND a."afterKg" = a."beforeKg" + a."deltaKg"
                  AND b."afterKg" = b."beforeKg" + b."deltaKg"
            ), transfer_keys AS (
                SELECT out_id AS ledger_id, out_id FROM transfer_pairs
                UNION ALL
                SELECT out_id + 1 AS ledger_id, out_id FROM transfer_pairs
            )
            SELECT v."CompanyId",
                CASE WHEN p.out_id IS NOT NULL THEN 'QC_RECLASS:' || p.out_id::text
                     WHEN t.out_id IS NOT NULL THEN 'LEGACY_TRANSFER:' || t.out_id::text ELSE v."MovementKey" END AS "MovementKey",
                v."MovementId", v."IsCanonicalMovement",
                CASE WHEN p.out_id IS NOT NULL THEN 'QC_WAIT_TO_RAW_PASS'
                     WHEN t.out_id IS NOT NULL THEN 'VERIFIED_LEGACY_TRANSFER' ELSE v."MovementType" END AS "MovementType",
                v."MovementAt", v."LedgerId", v."CreatedAt", v."Scope", v."SlotId", v."ShelfStockCode",
                v."ShelfStockId", v."ProductCode", v."LotNumber", v."StockType", v."UnitName",
                v."DeltaKg", v."BeforeKg", v."AfterKg", v."CreatedBy", v."RequestCode", v."AppSource",
                v."VoucherId", v."VoucherDetailId"
            FROM "Warehouse"."v_WarehouseMovementCanonical" v
            LEFT JOIN qc_keys p ON p.ledger_id = v."LedgerId" AND v."MovementId" IS NULL AND v."Scope" = 'NORMAL'
            LEFT JOIN transfer_keys t ON t.ledger_id = v."LedgerId" AND v."MovementId" IS NULL
            WHERE v."CompanyId" = {companyId}
            """).AsNoTracking();
}
