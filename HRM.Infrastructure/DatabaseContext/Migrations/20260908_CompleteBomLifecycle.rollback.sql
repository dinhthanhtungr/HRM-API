-- Manual rollback for 20260908_CompleteBomLifecycle.sql.
-- WARNING: dropping the snapshot table permanently removes recorded production-loss data.
DROP TABLE IF EXISTS manufacturing.mfg_production_order_losses;

ALTER TABLE IF EXISTS manufacturing.manufacturing_formulas
  DROP CONSTRAINT IF EXISTS fk_mfg_formulas_source_bom_version;

DROP INDEX IF EXISTS manufacturing.ix_mfg_formulas_source_bom_version_id;

ALTER TABLE IF EXISTS manufacturing.manufacturing_formulas
  DROP COLUMN IF EXISTS source_bom_version_id;

ALTER TABLE IF EXISTS bom.manufacturing_bom_loss_rules
  DROP CONSTRAINT IF EXISTS ck_manufacturing_bom_loss_rules_sequence_positive,
  DROP CONSTRAINT IF EXISTS ck_manufacturing_bom_loss_rules_rate_range,
  DROP CONSTRAINT IF EXISTS ck_manufacturing_bom_loss_rules_fixed_nonnegative,
  DROP CONSTRAINT IF EXISTS ck_manufacturing_bom_loss_rules_event_quantity_nonnegative,
  DROP CONSTRAINT IF EXISTS ck_manufacturing_bom_loss_rules_event_count_nonnegative;
