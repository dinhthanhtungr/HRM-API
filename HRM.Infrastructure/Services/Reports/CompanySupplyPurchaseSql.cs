namespace HRM.Infrastructure.Services.Reports;

internal static class CompanySupplyPurchaseSql
{
    internal const string Facts = """
        WITH eligible_orders AS (
            SELECT po.* FROM "Orders"."PurchaseOrders" po
            WHERE po."CompanyId" = @company AND po."IsActive"
              AND po."OrderType" IN ('PO','NCC')
              AND lower(coalesce(po."Status",'')) NOT IN ('canceled','cancelled')
              AND po."CreateDate" >= @from AND po."CreateDate" < @until
        ), view_items AS (
            -- The FULL JOIN in this view can repeat a PO value for invoice-code variants.
            -- Group the PO side only; do not filter on invoice existence or amount.
            SELECT v.po_no, v.item_code, min(v.po_amount_excl_vat) amount,
                max(v.po_qty) quantity,
                count(DISTINCT (v.po_amount_excl_vat, v.po_qty)) value_versions
            FROM purchase.v_po_ap_invoice_item_reconcile v
            WHERE v.company_id = @company
              AND EXISTS (SELECT 1 FROM eligible_orders po WHERE po."ExternalId" = v.po_no)
            GROUP BY v.po_no, v.item_code
        )
        SELECT 'PurchaseOrder'::text AS "Source", po."PurchaseOrderId"::text AS "DocumentKey",
            v.po_no::text AS "DocumentCode", po."PurchaseOrderId" AS "PurchaseOrderId",
            v.item_code::text AS "ItemCode", 'Supplies'::text AS "Kind", po."CreateDate" AS "Date",
            v.quantity AS "Quantity", ''::text AS "Unit",
            CASE WHEN v.value_versions <> 1 OR duplicate_po.po_count <> 1 THEN NULL ELSE v.amount END AS "Amount",
            CASE WHEN duplicate_po.po_count <> 1 THEN 'AmbiguousPurchaseOrder'
                 WHEN v.value_versions <> 1 THEN 'AmbiguousPurchaseAmount'
                 WHEN v.amount IS NULL THEN 'MissingOrderedAmount' END::text AS "Issue",
            true AS "IncludeQuantity"
        FROM view_items v
        JOIN eligible_orders po ON po."ExternalId" = v.po_no
        CROSS JOIN LATERAL (
            SELECT count(*) po_count FROM "Orders"."PurchaseOrders" p
            WHERE p."CompanyId" = @company AND p."IsActive" AND p."ExternalId" = v.po_no
        ) duplicate_po
        WHERE EXISTS (
            SELECT 1 FROM "Orders"."PurchaseOrderDetails" d
            LEFT JOIN "Material"."Materials" m ON m."MaterialId" = d."MaterialId" AND m."CompanyId" = @company
            LEFT JOIN "Material"."Categories" c ON c."CategoryId" = m."CategoryId" AND c."CompanyId" = @company
            WHERE d."PurchaseOrderId" = po."PurchaseOrderId" AND d."isActive"
              AND lower(d."MaterialExternalIDSnapshot"::text) = lower(v.item_code)
              AND (
                upper(split_part(v.item_code,'_',1)) IN ('VTSX','VTVP','VTAT','VTBB','VTBT','VTIT','VTNX','VTTH','VPP')
                OR (upper(split_part(v.item_code,'_',1)) = 'NVL'
                    AND upper(split_part(v.item_code,'_',2)) IN ('BB','VPP'))
                OR (c."Types" IN ('VTSX','VTVP','VTAT','VTBB','VTBT','VTIT','VTNX','VTTH','VPP')
                    AND upper(split_part(v.item_code,'_',1)) NOT IN ('DVTN','CPPBTT','CDC'))
              )
        )
          -- Preserve the PLPU cohort; this source is exclusively supplies purchases.
          AND NOT (po."OrderType" = 'NCC' AND v.item_code ILIKE 'NVL%'
              AND v.item_code NOT ILIKE '%VPP%' AND v.item_code NOT ILIKE '%BB%'
              AND coalesce(po."ExternalId",'') NOT ILIKE 'PO%')
        """;
}
