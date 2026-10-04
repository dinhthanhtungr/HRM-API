-- Routing edges stored with a process template and snapshotted into a BOM when applied.
ALTER TABLE bom.manufacturing_process_template_stages
    ADD COLUMN IF NOT EXISTS code citext NULL;

UPDATE bom.manufacturing_process_template_stages
SET code = external_id
WHERE code IS NULL OR btrim(code::text) = '';

ALTER TABLE bom.manufacturing_process_template_stages
    ALTER COLUMN code SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_process_template_stages_code
    ON bom.manufacturing_process_template_stages(manufacturing_process_template_id, code);

CREATE TABLE IF NOT EXISTS bom.manufacturing_process_template_stage_transitions
(
    manufacturing_process_template_stage_transition_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    manufacturing_process_template_id uuid NOT NULL,
    from_manufacturing_process_template_stage_id uuid NOT NULL,
    to_manufacturing_process_template_stage_id uuid NOT NULL,
    external_id citext NOT NULL,
    code citext NULL,
    transition_type integer NOT NULL,
    default_event_count integer NULL,
    sequence_no integer NOT NULL,
    note text NULL,
    CONSTRAINT fk_process_template_transitions_template
        FOREIGN KEY (manufacturing_process_template_id)
        REFERENCES bom.manufacturing_process_templates(manufacturing_process_template_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_process_template_transitions_from_stage
        FOREIGN KEY (from_manufacturing_process_template_stage_id)
        REFERENCES bom.manufacturing_process_template_stages(manufacturing_process_template_stage_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_process_template_transitions_to_stage
        FOREIGN KEY (to_manufacturing_process_template_stage_id)
        REFERENCES bom.manufacturing_process_template_stages(manufacturing_process_template_stage_id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_process_template_transitions_sequence_positive CHECK (sequence_no > 0),
    CONSTRAINT ck_process_template_transitions_distinct_stages CHECK (from_manufacturing_process_template_stage_id <> to_manufacturing_process_template_stage_id),
    CONSTRAINT ck_process_template_transitions_event_count_positive CHECK (default_event_count IS NULL OR default_event_count > 0),
    CONSTRAINT ck_process_template_transitions_type_valid CHECK (transition_type BETWEEN 1 AND 5)
);

ALTER TABLE bom.manufacturing_process_template_stage_transitions
    ADD COLUMN IF NOT EXISTS code citext NULL;

UPDATE bom.manufacturing_process_template_stage_transitions
SET code = external_id
WHERE code IS NULL OR btrim(code::text) = '';

ALTER TABLE bom.manufacturing_process_template_stage_transitions
    ALTER COLUMN code SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_process_template_transitions_external_id
    ON bom.manufacturing_process_template_stage_transitions(manufacturing_process_template_id, external_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_process_template_transitions_code
    ON bom.manufacturing_process_template_stage_transitions(manufacturing_process_template_id, code);
CREATE UNIQUE INDEX IF NOT EXISTS ux_process_template_transitions_sequence
    ON bom.manufacturing_process_template_stage_transitions(manufacturing_process_template_id, sequence_no);
CREATE UNIQUE INDEX IF NOT EXISTS ux_process_template_transitions_stage_pair
    ON bom.manufacturing_process_template_stage_transitions(manufacturing_process_template_id, from_manufacturing_process_template_stage_id, to_manufacturing_process_template_stage_id);
