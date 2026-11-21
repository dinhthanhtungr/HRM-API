namespace HRM.Infrastructure.Services.Reports;

internal static class CompanyPurchaseSql
{
    internal const string Facts = """
        WITH orders AS (
            SELECT * FROM "Orders"."PurchaseOrders"
            WHERE "CompanyId" = @company AND "OrderType" IN ('PO', 'NCC')
        ), item_codes AS (
            SELECT d."MaterialId", CASE WHEN count(DISTINCT d."MaterialExternalIDSnapshot") = 1
                THEN min(d."MaterialExternalIDSnapshot"::text) END code
            FROM "Orders"."PurchaseOrderDetails" d JOIN orders po USING ("PurchaseOrderId")
            GROUP BY d."MaterialId"
        ), item_types AS (
            SELECT codes."MaterialId", codes.code AS "ExternalId", coalesce(u."Name",m."Unit") AS "Unit",
                CASE WHEN upper(split_part(codes.code,'_',1)) = 'NVL'
                           AND upper(split_part(codes.code,'_',2)) = 'BB' THEN 'VTBB'
                     WHEN upper(split_part(codes.code,'_',1)) = 'NVL'
                           AND upper(split_part(codes.code,'_',2)) = 'VPP' THEN 'VTVP'
                     ELSE coalesce(c."Types", split_part(codes.code,'_',1)) END AS "Types"
            FROM item_codes codes
            LEFT JOIN "Material"."Materials" m ON m."MaterialId" = codes."MaterialId" AND m."CompanyId" = @company
            LEFT JOIN "Material"."Units" u ON u."UnitId"::text = m."Unit"
            LEFT JOIN "Material"."Categories" c ON c."CategoryId" = m."CategoryId" AND c."CompanyId" = @company
        ), items AS (
            SELECT *, CASE WHEN "Types" IN ('VTSX','VTVP','VTAT','VTBB','VTBT','VTIT','VTNX','VTTH','VPP') THEN 'Supplies'
                     WHEN "Types" = 'DVTN' THEN 'Services'
                     WHEN "Types" IN ('CPPBTT','CDC') THEN 'Other'
                     ELSE 'Unclassified' END kind
            FROM item_types
        ), receipts AS (
            SELECT 'MRO'::text source, h.stock_out_id::text doc_key, h.stock_out_code doc_code,
                i.purchase_order_id po_id, l.material_id item_id, l.material_externalid item_code,
                i.confirmed_at receipt_date, l.qty quantity, l.uom unit, false duplicate_source
            FROM mro.inbound_draft i
            JOIN mro.stock_out_hdr h USING (stock_out_id)
            JOIN mro.stock_out_line l USING (stock_out_id)
            JOIN orders po ON po."PurchaseOrderId" = i.purchase_order_id
            WHERE i.company_id = @company AND i.state = 'CONFIRMED'
              AND h.status = 'POSTED' AND h.source_type = 'PO_ALLOC'
              AND i.confirmed_at >= @from AND i.confirmed_at < @until AND l.qty > 0
            UNION ALL
            SELECT 'Warehouse', v."voucherId"::text, v."voucherCode",
                po."PurchaseOrderId", item_map.item_id, vd."productCode",
                ledger."createdAt", ledger."deltaKg", vd."UnitName",
                EXISTS (
                    SELECT 1 FROM mro.inbound_draft mi
                    JOIN mro.stock_out_hdr mh USING (stock_out_id)
                    JOIN mro.stock_out_line ml USING (stock_out_id)
                    WHERE mi.company_id = @company AND mi.purchase_order_id = po."PurchaseOrderId"
                      AND mi.state = 'CONFIRMED' AND mh.status = 'POSTED'
                      AND mh.source_type = 'PO_ALLOC' AND ml.material_id = item_map.item_id
                ) OR item_map.item_count > 1
                  OR (SELECT count(*) FROM orders other_po WHERE other_po."ExternalId" = po."ExternalId") > 1
            FROM "Warehouse"."WarehouseRequest" wr
            JOIN orders po ON po."ExternalId" = wr."codeFromRequest"
            JOIN "Warehouse"."WarehouseVouchers" v ON v."requestId" = wr."requestId" AND v."companyId" = @company
            JOIN "Warehouse"."WarehouseVoucherDetails" vd ON vd."voucherId" = v."voucherId"
            JOIN "Warehouse"."WarehouseShelfLedger" ledger
                ON ledger."voucherDetailId" = vd."voucherDetailId" AND ledger."companyId" = @company
            LEFT JOIN LATERAL (
                SELECT (array_agg(DISTINCT d."MaterialId"))[1] item_id,
                    count(DISTINCT d."MaterialId") item_count
                FROM "Orders"."PurchaseOrderDetails" d
                WHERE d."PurchaseOrderId" = po."PurchaseOrderId"
                    AND d."MaterialExternalIDSnapshot" = vd."productCode"
            ) item_map ON true
            WHERE wr."companyId" = @company AND wr."IsActive" AND v.status = 'APPLIED'
              AND v."voucherType" IN (5,9,13) AND ledger."deltaKg" > 0
              AND ledger."createdAt" >= @from AND ledger."createdAt" < @until
        ), facts AS (
            SELECT 'PurchaseOrder'::text AS "Source", po."PurchaseOrderId"::text AS "DocumentKey",
                coalesce(po."ExternalId", '') AS "DocumentCode", po."PurchaseOrderId" AS "PurchaseOrderId",
                coalesce(d."MaterialExternalIDSnapshot", m."ExternalId", '')::text AS "ItemCode",
                coalesce(m.kind, 'Unclassified') AS "Kind", po."CreateDate" AS "Date",
                coalesce(d."RequestQuantity",0) AS "Quantity", coalesce(m."Unit",'') AS "Unit",
                d."TotalPriceAgreed" AS "Amount",
                CASE WHEN d."TotalPriceAgreed" IS NULL THEN 'MissingOrderedAmount'
                     WHEN m.kind IS NULL OR m.kind = 'Unclassified' THEN 'UnclassifiedItem'
                     WHEN d."UnitPriceAgreed" = 0 THEN 'ZeroPrice' END AS "Issue",
                true AS "IncludeQuantity"
            FROM orders po
            JOIN "Orders"."PurchaseOrderDetails" d ON d."PurchaseOrderId" = po."PurchaseOrderId" AND d."isActive"
            LEFT JOIN items m ON m."MaterialId" = d."MaterialId"
            WHERE po."IsActive" AND lower(coalesce(po."Status",'')) NOT IN ('canceled','cancelled')
              AND po."CreateDate" >= @from AND po."CreateDate" < @until
              AND coalesce(m."Types",'') <> 'NVL'
              -- Never recategorize the existing PLPU raw-material cohort.
              AND NOT (po."OrderType" = 'NCC' AND coalesce(d."MaterialExternalIDSnapshot",'') ILIKE 'NVL%'
                  AND coalesce(d."MaterialExternalIDSnapshot",'') NOT ILIKE '%VPP%'
                  AND coalesce(d."MaterialExternalIDSnapshot",'') NOT ILIKE '%BB%'
                  AND coalesce(po."ExternalId",'') NOT ILIKE 'PO%')
            UNION ALL
            SELECT r.source, r.doc_key, coalesce(r.doc_code,''), r.po_id,
                coalesce(r.item_code,''), coalesce(m.kind,'Unclassified'), r.receipt_date, r.quantity,
                coalesce(receipt_unit."Name",r.unit,''),
                CASE WHEN r.duplicate_source OR price.prices <> 1 OR price.unit_price IS NULL OR price.missing_prices > 0
                          OR nullif(trim(r.unit),'') IS NULL
                          OR (m."Unit" IS NOT NULL AND lower(trim(coalesce(receipt_unit."Name",r.unit)))
                              IS DISTINCT FROM lower(trim(m."Unit")))
                     THEN NULL ELSE round(r.quantity * price.unit_price,2) END,
                CASE WHEN r.duplicate_source THEN 'PossibleDuplicateSource'
                     WHEN price.lines = 0 THEN 'MissingPurchaseLine'
                     WHEN price.prices = 0 OR price.missing_prices > 0 THEN 'MissingPrice'
                     WHEN price.prices > 1 THEN 'AmbiguousPrice'
                     WHEN nullif(trim(r.unit),'') IS NULL THEN 'MissingUnit'
                     WHEN m."Unit" IS NOT NULL AND lower(trim(coalesce(receipt_unit."Name",r.unit)))
                          IS DISTINCT FROM lower(trim(m."Unit")) THEN 'UnitMismatch'
                     WHEN NOT po."IsActive" OR price.active_lines = 0 THEN 'InactivePurchase'
                     WHEN m.kind IS NULL OR m.kind = 'Unclassified' THEN 'UnclassifiedItem'
                     WHEN price.unit_price = 0 THEN 'ZeroPrice' END,
                NOT r.duplicate_source
            FROM receipts r
            JOIN orders po ON po."PurchaseOrderId" = r.po_id
            LEFT JOIN items m ON m."MaterialId" = r.item_id
            LEFT JOIN "Material"."Units" receipt_unit ON receipt_unit."UnitId"::text = r.unit
            LEFT JOIN LATERAL (
                SELECT count(*) lines, count(DISTINCT d."UnitPriceAgreed") prices,
                    min(d."UnitPriceAgreed") unit_price, count(*) FILTER (WHERE d."isActive") active_lines,
                    count(*) FILTER (WHERE d."UnitPriceAgreed" IS NULL) missing_prices
                FROM "Orders"."PurchaseOrderDetails" d
                WHERE d."PurchaseOrderId" = r.po_id AND d."MaterialId" = r.item_id
            ) price ON true
            WHERE coalesce(m."Types",'') <> 'NVL'
              AND NOT (po."OrderType" = 'NCC' AND coalesce(r.item_code,'') ILIKE 'NVL%'
                  AND coalesce(r.item_code,'') NOT ILIKE '%VPP%' AND coalesce(r.item_code,'') NOT ILIKE '%BB%'
                  AND coalesce(po."ExternalId",'') NOT ILIKE 'PO%')
        ) SELECT * FROM facts
        """;
}
