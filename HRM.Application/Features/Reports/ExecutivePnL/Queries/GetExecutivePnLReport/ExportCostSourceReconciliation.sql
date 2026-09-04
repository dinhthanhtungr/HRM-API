/*
  ĐỐI SOÁT NGUỒN GIÁ VỐN EXECUTIVE P&L (PostgreSQL, chỉ đọc).
  Đổi from_date / to_date_exclusive / company_id và selected_mode trong params.
  selected_mode: FormulaSnapshot | WarehouseActual.

  Công thức đúng với ExecutivePnLDeliveryCostResolver:
  FormulaSnapshot = SUM(FormulaMaterials.quantity * FormulaMaterials.unit_price), item_type = 0.
  WarehouseActual = SUM(OperationMaterialBuffer.AmountCost)
                    / SUM(ProductionOutputReceiptSource.ProducedQtyKg).
*/
WITH params AS (
    SELECT
        DATE '2026-07-01' AS from_date,
        DATE '2026-08-01' AS to_date_exclusive,
        NULL::uuid AS company_id, -- thay UUID công ty hoặc để NULL để lấy tất cả
        'WarehouseActual'::text AS selected_mode
),
delivery_lines AS (
    SELECT
        dod."ID" AS delivery_order_detail_id,
        doo."ExternalId" AS delivery_code,
        doo."CreatedDate" AS delivery_date,
        doo."CompanyId" AS company_id,
        dod."MerchandiseOrderDetailId" AS merchandise_order_detail_id,
        mod."FormulaId" AS formula_id,
        mod."ProductExternalIdSnapshot" AS product_code,
        dod."Quantity" AS delivery_quantity,
        mod."BaseCostSnapshot" AS base_cost_snapshot,
        COALESCE(lot.lot_cost_snapshot_amount, 0) AS lot_cost_snapshot_amount,
        COALESCE(lot.has_normalized_lots, false) AS has_normalized_lots
    FROM "DeliveryOrder"."DeliveryOrderDetail" dod
    JOIN "DeliveryOrder"."DeliveryOrders" doo
      ON doo."ID" = dod."DeliveryOrderId"
    JOIN "Orders"."MerchandiseOrderDetails" mod
      ON mod."MerchandiseOrderDetailId" = dod."MerchandiseOrderDetailId"
    LEFT JOIN LATERAL (
        SELECT true AS has_normalized_lots, SUM(lc."TotalCostSnapshot") AS lot_cost_snapshot_amount
        FROM "DeliveryOrder"."DeliveryOrderDetailLotConsumptions" lc
        WHERE lc."DeliveryOrderDetailId" = dod."ID" AND lc."IsActive" = true
    ) lot ON true
    CROSS JOIN params p
    WHERE dod."IsActive" = true
      AND doo."IsActive" = true
      AND doo."Status" <> 'Canceled'
      AND doo."CustomerExternalIdSnapShot" <> 'KH_VIETAUS'
      AND doo."CreatedDate" >= p.from_date
      AND doo."CreatedDate" < p.to_date_exclusive
      AND (p.company_id IS NULL OR doo."CompanyId" = p.company_id)
),
formula_cost AS (
    SELECT fm."FormulaId" AS formula_id,
           SUM(fm.quantity * fm.unit_price) AS formula_snapshot_unit_cost
    FROM "SampleRequests"."FormulaMaterials" fm
    WHERE fm."IsActive" = true AND fm.item_type = 0 -- ItemType.Material
    GROUP BY fm."FormulaId"
),
valid_mfg_links AS (
    SELECT DISTINCT dl.merchandise_order_detail_id,
           mpo."mfgProductionOrderId" AS mfg_production_order_id,
           mpo.external_id AS mfg_external_id
    FROM delivery_lines dl
    JOIN "manufacturing"."MfgOrderPOs" link
      ON link."MerchandiseOrderDetailId" = dl.merchandise_order_detail_id
     AND link."IsActive" = true
    JOIN "manufacturing"."MfgProductionOrders" mpo
      ON mpo."mfgProductionOrderId" = link."MfgProductionOrderId"
    CROSS JOIN params p
    WHERE p.company_id IS NULL OR mpo.company_id = p.company_id
),
output_qty AS (
    SELECT pos."MfgProductionOrderId" AS mfg_production_order_id,
           SUM(pos."ProducedQtyKg") AS produced_qty_kg
    FROM "Warehouse"."ProductionOutputReceiptSource" pos
    CROSS JOIN params p
    WHERE pos."MfgProductionOrderId" IS NOT NULL
      AND pos."ProducedQtyKg" > 0
      AND (p.company_id IS NULL OR pos."companyId" = p.company_id)
    GROUP BY pos."MfgProductionOrderId"
),
material_cost AS (
    SELECT omb."MfgProductionOrderId" AS mfg_production_order_id,
           SUM(omb."AmountCost") AS material_amount_cost
    FROM "Warehouse"."OperationMaterialBuffer" omb
    CROSS JOIN params p
    WHERE omb."MfgProductionOrderId" IS NOT NULL
      AND omb."AmountCost" > 0
      AND (p.company_id IS NULL OR omb."companyId" = p.company_id)
    GROUP BY omb."MfgProductionOrderId"
),
mfg_cost AS (
    SELECT link.merchandise_order_detail_id, link.mfg_production_order_id, link.mfg_external_id,
           oq.produced_qty_kg, mc.material_amount_cost,
           CASE WHEN oq.produced_qty_kg > 0 AND mc.material_amount_cost > 0
                THEN mc.material_amount_cost / oq.produced_qty_kg END AS warehouse_actual_unit_cost
    FROM valid_mfg_links link
    LEFT JOIN output_qty oq ON oq.mfg_production_order_id = link.mfg_production_order_id
    LEFT JOIN material_cost mc ON mc.mfg_production_order_id = link.mfg_production_order_id
),
mfg_by_order_detail AS (
    SELECT merchandise_order_detail_id,
           COUNT(DISTINCT mfg_production_order_id) AS active_mfg_count,
           STRING_AGG(mfg_external_id::text, ', ' ORDER BY mfg_external_id::text) AS mfg_external_ids,
           MAX(produced_qty_kg) AS produced_qty_kg,
           MAX(material_amount_cost) AS material_amount_cost,
           MAX(warehouse_actual_unit_cost) AS warehouse_actual_unit_cost
    FROM mfg_cost
    GROUP BY merchandise_order_detail_id
)
SELECT
    dl.delivery_date, dl.delivery_code, dl.delivery_order_detail_id, dl.product_code,
    dl.delivery_quantity, dl.merchandise_order_detail_id, dl.formula_id,
    COALESCE(mfg.active_mfg_count, 0) AS active_mfg_count,
    mfg.mfg_external_ids, mfg.produced_qty_kg, mfg.material_amount_cost,
    mfg.warehouse_actual_unit_cost, fc.formula_snapshot_unit_cost,
    dl.base_cost_snapshot, dl.lot_cost_snapshot_amount,
    CASE
      WHEN mfg.active_mfg_count IS NULL THEN 'NO_ACTIVE_MFG_LINK'
      WHEN mfg.active_mfg_count <> 1 THEN 'MULTIPLE_ACTIVE_MFG_LINKS'
      WHEN mfg.produced_qty_kg IS NULL THEN 'NO_PRODUCED_QTY'
      WHEN mfg.material_amount_cost IS NULL THEN 'NO_MATERIAL_AMOUNT_COST'
      WHEN mfg.warehouse_actual_unit_cost IS NULL THEN 'WAREHOUSE_COST_NOT_READY'
      ELSE 'WAREHOUSE_ACTUAL_READY'
    END AS warehouse_actual_check,
    CASE
      WHEN p.selected_mode = 'WarehouseActual'
       AND mfg.active_mfg_count = 1 AND mfg.warehouse_actual_unit_cost IS NOT NULL THEN 'WarehouseActual'
      WHEN fc.formula_snapshot_unit_cost IS NOT NULL THEN 'FormulaSnapshot'
      WHEN dl.has_normalized_lots AND dl.lot_cost_snapshot_amount > 0 THEN 'LegacyLotSnapshot'
      ELSE 'BaseCostSnapshot'
    END AS cost_source_used,
    CASE
      WHEN p.selected_mode = 'WarehouseActual'
       AND mfg.active_mfg_count = 1 AND mfg.warehouse_actual_unit_cost IS NOT NULL
        THEN dl.delivery_quantity * mfg.warehouse_actual_unit_cost
      WHEN fc.formula_snapshot_unit_cost IS NOT NULL THEN dl.delivery_quantity * fc.formula_snapshot_unit_cost
      WHEN dl.has_normalized_lots AND dl.lot_cost_snapshot_amount > 0 THEN dl.lot_cost_snapshot_amount
      ELSE dl.delivery_quantity * dl.base_cost_snapshot
    END AS report_cost_amount,
    dl.delivery_quantity * COALESCE(fc.formula_snapshot_unit_cost, 0) AS formula_snapshot_cost_amount,
    dl.delivery_quantity * COALESCE(mfg.warehouse_actual_unit_cost, 0) AS warehouse_actual_cost_amount
FROM delivery_lines dl
LEFT JOIN formula_cost fc ON fc.formula_id = dl.formula_id
LEFT JOIN mfg_by_order_detail mfg ON mfg.merchandise_order_detail_id = dl.merchandise_order_detail_id
CROSS JOIN params p
ORDER BY dl.delivery_date, dl.delivery_code, dl.delivery_order_detail_id;
