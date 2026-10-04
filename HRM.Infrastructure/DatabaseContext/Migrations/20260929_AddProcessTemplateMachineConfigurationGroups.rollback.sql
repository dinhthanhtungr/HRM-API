DROP INDEX IF EXISTS bom.ix_process_template_stage_machines_configuration_group;

ALTER TABLE bom.manufacturing_process_template_stage_machines
    DROP CONSTRAINT IF EXISTS ck_process_template_stage_machines_group_name_requires_key,
    DROP COLUMN IF EXISTS configuration_group_name,
    DROP COLUMN IF EXISTS configuration_group_key;
