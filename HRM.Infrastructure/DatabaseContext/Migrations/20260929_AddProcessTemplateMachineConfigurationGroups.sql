-- UI grouping metadata for machines that share the same operating configuration.
-- Machines and their parameters remain stored as independent rows.
ALTER TABLE bom.manufacturing_process_template_stage_machines
    ADD COLUMN IF NOT EXISTS configuration_group_key uuid NULL,
    ADD COLUMN IF NOT EXISTS configuration_group_name character varying(200) NULL;

ALTER TABLE bom.manufacturing_process_template_stage_machines
    DROP CONSTRAINT IF EXISTS ck_process_template_stage_machines_group_name_requires_key;

ALTER TABLE bom.manufacturing_process_template_stage_machines
    ADD CONSTRAINT ck_process_template_stage_machines_group_name_requires_key
    CHECK (configuration_group_name IS NULL OR configuration_group_key IS NOT NULL);

CREATE INDEX IF NOT EXISTS ix_process_template_stage_machines_configuration_group
    ON bom.manufacturing_process_template_stage_machines
    (manufacturing_process_template_stage_id, configuration_group_key);
