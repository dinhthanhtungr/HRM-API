BEGIN;

-- This rollback intentionally fails if value-less rows already exist. Fill or remove those
-- rows before restoring the old database-level requirement.
ALTER TABLE bom.manufacturing_process_template_stage_machine_parameters
    ADD CONSTRAINT ck_process_template_machine_parameters_has_value
    CHECK (target_value IS NOT NULL OR min_value IS NOT NULL OR max_value IS NOT NULL);

ALTER TABLE bom.manufacturing_bom_stage_machine_parameters
    ADD CONSTRAINT ck_bom_stage_machine_parameters_has_value
    CHECK (target_value_snapshot IS NOT NULL OR min_value_snapshot IS NOT NULL OR max_value_snapshot IS NOT NULL);

COMMIT;
