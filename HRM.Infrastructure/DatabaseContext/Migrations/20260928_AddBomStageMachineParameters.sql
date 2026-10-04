-- Configurable machine operating parameters in process templates and immutable M-BOM snapshots.
CREATE TABLE IF NOT EXISTS bom.manufacturing_process_template_stage_machine_parameters
(
    manufacturing_process_template_stage_machine_parameter_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    manufacturing_process_template_stage_machine_id uuid NOT NULL,
    parameter_code citext NOT NULL,
    parameter_name character varying(200) NOT NULL,
    target_value numeric(18,6) NULL,
    min_value numeric(18,6) NULL,
    max_value numeric(18,6) NULL,
    unit citext NOT NULL,
    is_required boolean NOT NULL DEFAULT false,
    sequence_no integer NOT NULL,
    note text NULL,
    CONSTRAINT fk_process_template_machine_parameters_machine
        FOREIGN KEY (manufacturing_process_template_stage_machine_id)
        REFERENCES bom.manufacturing_process_template_stage_machines(manufacturing_process_template_stage_machine_id)
        ON DELETE CASCADE,
    CONSTRAINT ck_process_template_machine_parameters_sequence_positive CHECK (sequence_no > 0),
    CONSTRAINT ck_process_template_machine_parameters_has_value CHECK (target_value IS NOT NULL OR min_value IS NOT NULL OR max_value IS NOT NULL),
    CONSTRAINT ck_process_template_machine_parameters_range CHECK
    (
        (min_value IS NULL OR max_value IS NULL OR min_value <= max_value)
        AND (target_value IS NULL OR min_value IS NULL OR target_value >= min_value)
        AND (target_value IS NULL OR max_value IS NULL OR target_value <= max_value)
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_process_template_machine_parameters_code
    ON bom.manufacturing_process_template_stage_machine_parameters(manufacturing_process_template_stage_machine_id, parameter_code);
CREATE UNIQUE INDEX IF NOT EXISTS ux_process_template_machine_parameters_sequence
    ON bom.manufacturing_process_template_stage_machine_parameters(manufacturing_process_template_stage_machine_id, sequence_no);

CREATE TABLE IF NOT EXISTS bom.manufacturing_bom_stage_machine_parameters
(
    manufacturing_bom_stage_machine_parameter_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    manufacturing_bom_stage_machine_id uuid NOT NULL,
    parameter_code_snapshot citext NOT NULL,
    parameter_name_snapshot character varying(200) NOT NULL,
    target_value_snapshot numeric(18,6) NULL,
    min_value_snapshot numeric(18,6) NULL,
    max_value_snapshot numeric(18,6) NULL,
    unit_snapshot citext NOT NULL,
    is_required_snapshot boolean NOT NULL,
    sequence_no integer NOT NULL,
    note_snapshot text NULL,
    CONSTRAINT fk_bom_stage_machine_parameters_machine
        FOREIGN KEY (manufacturing_bom_stage_machine_id)
        REFERENCES bom.manufacturing_bom_stage_machines(manufacturing_bom_stage_machine_id)
        ON DELETE CASCADE,
    CONSTRAINT ck_bom_stage_machine_parameters_sequence_positive CHECK (sequence_no > 0),
    CONSTRAINT ck_bom_stage_machine_parameters_has_value CHECK (target_value_snapshot IS NOT NULL OR min_value_snapshot IS NOT NULL OR max_value_snapshot IS NOT NULL),
    CONSTRAINT ck_bom_stage_machine_parameters_range CHECK
    (
        (min_value_snapshot IS NULL OR max_value_snapshot IS NULL OR min_value_snapshot <= max_value_snapshot)
        AND (target_value_snapshot IS NULL OR min_value_snapshot IS NULL OR target_value_snapshot >= min_value_snapshot)
        AND (target_value_snapshot IS NULL OR max_value_snapshot IS NULL OR target_value_snapshot <= max_value_snapshot)
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_bom_stage_machine_parameters_code
    ON bom.manufacturing_bom_stage_machine_parameters(manufacturing_bom_stage_machine_id, parameter_code_snapshot);
CREATE UNIQUE INDEX IF NOT EXISTS ux_bom_stage_machine_parameters_sequence
    ON bom.manufacturing_bom_stage_machine_parameters(manufacturing_bom_stage_machine_id, sequence_no);
