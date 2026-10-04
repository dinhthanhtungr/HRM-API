BEGIN;

-- Process templates define which parameters exist. Their operating values are optional.
ALTER TABLE bom.manufacturing_process_template_stage_machine_parameters
    DROP CONSTRAINT IF EXISTS ck_process_template_machine_parameters_has_value;

-- Applying a template creates an M-BOM Draft snapshot before operators enter actual values.
-- Required values are enforced by the M-BOM release use case.
ALTER TABLE bom.manufacturing_bom_stage_machine_parameters
    DROP CONSTRAINT IF EXISTS ck_bom_stage_machine_parameters_has_value;

COMMIT;
