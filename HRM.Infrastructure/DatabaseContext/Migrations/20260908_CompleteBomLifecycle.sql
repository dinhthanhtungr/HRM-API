-- Completes database objects required by BOM lifecycle and production-loss snapshots.
ALTER TABLE manufacturing.manufacturing_formulas
  ADD COLUMN IF NOT EXISTS source_bom_version_id uuid NULL;

CREATE INDEX IF NOT EXISTS ix_mfg_formulas_source_bom_version_id
  ON manufacturing.manufacturing_formulas(source_bom_version_id);

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_mfg_formulas_source_bom_version') THEN
    ALTER TABLE manufacturing.manufacturing_formulas
      ADD CONSTRAINT fk_mfg_formulas_source_bom_version
      FOREIGN KEY (source_bom_version_id) REFERENCES bom.bom_versions(bom_version_id) ON DELETE RESTRICT;
  END IF;
END $$;

CREATE TABLE IF NOT EXISTS manufacturing.mfg_production_order_losses (
  mfg_production_order_loss_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  company_id uuid NOT NULL,
  mfg_production_order_id uuid NOT NULL,
  source_manufacturing_bom_loss_rule_id uuid NULL,
  loss_type_code_snapshot citext NOT NULL,
  loss_type_name_snapshot varchar(200) NOT NULL,
  calculation_method_snapshot citext NOT NULL,
  stage_code_snapshot citext NULL,
  material_code_snapshot citext NULL,
  rate_percent_snapshot numeric(9,6) NULL,
  fixed_quantity_kg_snapshot numeric(18,3) NULL,
  quantity_per_event_kg_snapshot numeric(18,3) NULL,
  include_in_material_request_snapshot boolean NOT NULL DEFAULT false,
  planned_quantity_kg numeric(18,3) NOT NULL DEFAULT 0,
  actual_quantity_kg numeric(18,3) NULL,
  event_count integer NULL,
  recovered_quantity_kg numeric(18,3) NOT NULL DEFAULT 0,
  is_finalized boolean NOT NULL DEFAULT false,
  note text NULL,
  recorded_date timestamp without time zone NOT NULL,
  recorded_by uuid NOT NULL,
  updated_date timestamp without time zone NULL,
  updated_by uuid NULL,
  CONSTRAINT ck_mfg_production_order_losses_planned_nonnegative CHECK (planned_quantity_kg >= 0),
  CONSTRAINT ck_mfg_production_order_losses_actual_nonnegative CHECK (actual_quantity_kg IS NULL OR actual_quantity_kg >= 0),
  CONSTRAINT ck_mfg_production_order_losses_event_count_nonnegative CHECK (event_count IS NULL OR event_count >= 0),
  CONSTRAINT ck_mfg_production_order_losses_recovered_nonnegative CHECK (recovered_quantity_kg >= 0),
  CONSTRAINT fk_mfg_production_order_losses_company FOREIGN KEY (company_id) REFERENCES company."Companies"("CompanyId") ON DELETE RESTRICT,
  CONSTRAINT fk_mfg_production_order_losses_order FOREIGN KEY (mfg_production_order_id) REFERENCES manufacturing."MfgProductionOrders"("MfgProductionOrderId") ON DELETE RESTRICT,
  CONSTRAINT fk_mfg_production_order_losses_source_rule FOREIGN KEY (source_manufacturing_bom_loss_rule_id) REFERENCES bom.manufacturing_bom_loss_rules(manufacturing_bom_loss_rule_id) ON DELETE SET NULL,
  CONSTRAINT fk_mfg_production_order_losses_recorded_by FOREIGN KEY (recorded_by) REFERENCES hr."Employees"("EmployeeId") ON DELETE RESTRICT,
  CONSTRAINT fk_mfg_production_order_losses_updated_by FOREIGN KEY (updated_by) REFERENCES hr."Employees"("EmployeeId") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_mfg_production_order_losses_company_order
  ON manufacturing.mfg_production_order_losses(company_id, mfg_production_order_id);
CREATE INDEX IF NOT EXISTS ix_mfg_production_order_losses_order_finalized
  ON manufacturing.mfg_production_order_losses(mfg_production_order_id, is_finalized);
CREATE INDEX IF NOT EXISTS ix_mfg_production_order_losses_source_rule
  ON manufacturing.mfg_production_order_losses(source_manufacturing_bom_loss_rule_id);
CREATE INDEX IF NOT EXISTS ix_mfg_production_order_losses_type_code
  ON manufacturing.mfg_production_order_losses(loss_type_code_snapshot);
CREATE UNIQUE INDEX IF NOT EXISTS ux_mfg_production_order_losses_order_source_rule
  ON manufacturing.mfg_production_order_losses(mfg_production_order_id, source_manufacturing_bom_loss_rule_id)
  WHERE source_manufacturing_bom_loss_rule_id IS NOT NULL;

ALTER TABLE manufacturing.mfg_production_order_losses
  ADD COLUMN IF NOT EXISTS include_in_material_request_snapshot boolean NOT NULL DEFAULT false;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_manufacturing_bom_loss_rules_sequence_positive') THEN
    ALTER TABLE bom.manufacturing_bom_loss_rules ADD CONSTRAINT ck_manufacturing_bom_loss_rules_sequence_positive CHECK (sequence_no > 0);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_manufacturing_bom_loss_rules_rate_range') THEN
    ALTER TABLE bom.manufacturing_bom_loss_rules ADD CONSTRAINT ck_manufacturing_bom_loss_rules_rate_range CHECK (rate_percent IS NULL OR (rate_percent >= 0 AND rate_percent < 100));
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_manufacturing_bom_loss_rules_fixed_nonnegative') THEN
    ALTER TABLE bom.manufacturing_bom_loss_rules ADD CONSTRAINT ck_manufacturing_bom_loss_rules_fixed_nonnegative CHECK (fixed_quantity_kg IS NULL OR fixed_quantity_kg >= 0);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_manufacturing_bom_loss_rules_event_quantity_nonnegative') THEN
    ALTER TABLE bom.manufacturing_bom_loss_rules ADD CONSTRAINT ck_manufacturing_bom_loss_rules_event_quantity_nonnegative CHECK (quantity_per_event_kg IS NULL OR quantity_per_event_kg >= 0);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_manufacturing_bom_loss_rules_event_count_nonnegative') THEN
    ALTER TABLE bom.manufacturing_bom_loss_rules ADD CONSTRAINT ck_manufacturing_bom_loss_rules_event_count_nonnegative CHECK (default_event_count IS NULL OR default_event_count >= 0);
  END IF;
END $$;
