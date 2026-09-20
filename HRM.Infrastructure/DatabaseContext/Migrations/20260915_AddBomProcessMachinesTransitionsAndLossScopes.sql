CREATE TABLE IF NOT EXISTS bom.manufacturing_bom_stage_machines (
    manufacturing_bom_stage_machine_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    manufacturing_bom_stage_id uuid NOT NULL REFERENCES bom.manufacturing_bom_stages(manufacturing_bom_stage_id) ON DELETE RESTRICT,
    equipment_id integer NOT NULL REFERENCES mro.equipment(equipment_id) ON DELETE RESTRICT,
    equipment_externalid_snapshot text NOT NULL,
    equipment_name_snapshot text NOT NULL,
    is_default boolean NOT NULL DEFAULT false,
    sequence_no integer NOT NULL,
    note text NULL,
    CONSTRAINT ck_manufacturing_bom_stage_machines_sequence_positive CHECK (sequence_no > 0),
    CONSTRAINT ux_manufacturing_bom_stage_machines_stage_equipment UNIQUE (manufacturing_bom_stage_id, equipment_id)
);

CREATE TABLE IF NOT EXISTS bom.manufacturing_bom_stage_transitions (
    manufacturing_bom_stage_transition_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    bom_version_id uuid NOT NULL REFERENCES bom.bom_versions(bom_version_id) ON DELETE RESTRICT,
    from_manufacturing_bom_stage_id uuid NOT NULL REFERENCES bom.manufacturing_bom_stages(manufacturing_bom_stage_id) ON DELETE RESTRICT,
    to_manufacturing_bom_stage_id uuid NOT NULL REFERENCES bom.manufacturing_bom_stages(manufacturing_bom_stage_id) ON DELETE RESTRICT,
    code citext NOT NULL,
    transition_type citext NOT NULL,
    default_event_count integer NULL,
    sequence_no integer NOT NULL,
    note text NULL,
    CONSTRAINT ck_manufacturing_bom_stage_transitions_sequence_positive CHECK (sequence_no > 0),
    CONSTRAINT ck_manufacturing_bom_stage_transitions_distinct_stages CHECK (from_manufacturing_bom_stage_id <> to_manufacturing_bom_stage_id),
    CONSTRAINT ux_manufacturing_bom_stage_transitions_version_code UNIQUE (bom_version_id, code),
    CONSTRAINT ux_manufacturing_bom_stage_transitions_version_sequence UNIQUE (bom_version_id, sequence_no)
);

ALTER TABLE bom.manufacturing_bom_loss_rules
    ADD COLUMN IF NOT EXISTS manufacturing_bom_stage_transition_id uuid NULL,
    ADD COLUMN IF NOT EXISTS scope citext NOT NULL DEFAULT 'Material',
    ADD COLUMN IF NOT EXISTS allocation_method citext NOT NULL DEFAULT 'DirectMaterial';

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_manufacturing_bom_loss_rules_transition') THEN
        ALTER TABLE bom.manufacturing_bom_loss_rules
            ADD CONSTRAINT fk_manufacturing_bom_loss_rules_transition
            FOREIGN KEY (manufacturing_bom_stage_transition_id)
            REFERENCES bom.manufacturing_bom_stage_transitions(manufacturing_bom_stage_transition_id)
            ON DELETE RESTRICT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_manufacturing_bom_loss_rules_transition
    ON bom.manufacturing_bom_loss_rules(manufacturing_bom_stage_transition_id);
