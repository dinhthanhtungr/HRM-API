CREATE TABLE IF NOT EXISTS bom.manufacturing_work_instruction_templates (
    manufacturing_work_instruction_template_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES company."Companies"("companyId") ON DELETE RESTRICT,
    external_id citext NOT NULL,
    name varchar(200) NOT NULL,
    version_no integer NOT NULL,
    status citext NOT NULL,
    purpose text NULL,
    preparation text NULL,
    procedure text NOT NULL,
    quality_requirements text NULL,
    safety_notes text NULL,
    effective_from timestamp without time zone NULL,
    effective_to timestamp without time zone NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_date timestamp without time zone NOT NULL,
    created_by uuid NOT NULL REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
    updated_date timestamp without time zone NULL,
    updated_by uuid NULL REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
    released_date timestamp without time zone NULL,
    released_by uuid NULL REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
    CONSTRAINT ck_manufacturing_work_instruction_templates_effective_range CHECK (effective_to IS NULL OR effective_from IS NULL OR effective_to >= effective_from),
    CONSTRAINT ux_work_instruction_templates_company_external_version UNIQUE (company_id, external_id, version_no)
);

CREATE INDEX IF NOT EXISTS ix_work_instruction_templates_company_status
    ON bom.manufacturing_work_instruction_templates(company_id, status);

CREATE TABLE IF NOT EXISTS bom.manufacturing_work_instruction_checklist_items (
    manufacturing_work_instruction_checklist_item_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    manufacturing_work_instruction_template_id uuid NOT NULL REFERENCES bom.manufacturing_work_instruction_templates(manufacturing_work_instruction_template_id) ON DELETE CASCADE,
    external_id citext NOT NULL,
    content text NOT NULL,
    sequence_no integer NOT NULL,
    is_required boolean NOT NULL,
    expected_value varchar(200) NULL,
    unit varchar(32) NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT ck_work_instruction_checklist_sequence_positive CHECK (sequence_no > 0),
    CONSTRAINT ux_work_instruction_checklist_external_id UNIQUE (manufacturing_work_instruction_template_id, external_id),
    CONSTRAINT ux_work_instruction_checklist_sequence UNIQUE (manufacturing_work_instruction_template_id, sequence_no)
);

CREATE TABLE IF NOT EXISTS bom.manufacturing_process_templates (
    manufacturing_process_template_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES company."Companies"("companyId") ON DELETE RESTRICT,
    external_id citext NOT NULL,
    name varchar(200) NOT NULL,
    description text NULL,
    version_no integer NOT NULL,
    status citext NOT NULL,
    effective_from timestamp without time zone NULL,
    effective_to timestamp without time zone NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_date timestamp without time zone NOT NULL,
    created_by uuid NOT NULL REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
    updated_date timestamp without time zone NULL,
    updated_by uuid NULL REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
    released_date timestamp without time zone NULL,
    released_by uuid NULL REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
    CONSTRAINT ck_manufacturing_process_templates_effective_range CHECK (effective_to IS NULL OR effective_from IS NULL OR effective_to >= effective_from),
    CONSTRAINT ux_process_templates_company_external_version UNIQUE (company_id, external_id, version_no)
);

CREATE INDEX IF NOT EXISTS ix_process_templates_company_status
    ON bom.manufacturing_process_templates(company_id, status);

CREATE TABLE IF NOT EXISTS bom.manufacturing_process_template_stages (
    manufacturing_process_template_stage_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    manufacturing_process_template_id uuid NOT NULL REFERENCES bom.manufacturing_process_templates(manufacturing_process_template_id) ON DELETE CASCADE,
    manufacturing_work_instruction_template_id uuid NULL REFERENCES bom.manufacturing_work_instruction_templates(manufacturing_work_instruction_template_id) ON DELETE RESTRICT,
    external_id citext NOT NULL,
    name varchar(200) NOT NULL,
    sequence_no integer NOT NULL,
    description text NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT ck_process_template_stages_sequence_positive CHECK (sequence_no > 0),
    CONSTRAINT ux_process_template_stages_external_id UNIQUE (manufacturing_process_template_id, external_id),
    CONSTRAINT ux_process_template_stages_sequence UNIQUE (manufacturing_process_template_id, sequence_no)
);

CREATE TABLE IF NOT EXISTS bom.manufacturing_bom_stage_work_instructions (
    manufacturing_bom_stage_work_instruction_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    manufacturing_bom_stage_id uuid NOT NULL REFERENCES bom.manufacturing_bom_stages(manufacturing_bom_stage_id) ON DELETE CASCADE,
    source_work_instruction_template_id uuid NULL REFERENCES bom.manufacturing_work_instruction_templates(manufacturing_work_instruction_template_id) ON DELETE SET NULL,
    external_id_snapshot varchar(64) NOT NULL,
    name_snapshot varchar(200) NOT NULL,
    version_no_snapshot integer NOT NULL,
    purpose_snapshot text NULL,
    preparation_snapshot text NULL,
    procedure_snapshot text NOT NULL,
    quality_requirements_snapshot text NULL,
    safety_notes_snapshot text NULL,
    snapshotted_date timestamp without time zone NOT NULL,
    CONSTRAINT ux_bom_stage_work_instructions_stage UNIQUE (manufacturing_bom_stage_id)
);

CREATE TABLE IF NOT EXISTS bom.manufacturing_process_template_stage_machines (
    manufacturing_process_template_stage_machine_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    manufacturing_process_template_stage_id uuid NOT NULL REFERENCES bom.manufacturing_process_template_stages(manufacturing_process_template_stage_id) ON DELETE CASCADE,
    equipment_id integer NOT NULL REFERENCES mro.equipment(equipment_id) ON DELETE RESTRICT,
    is_default boolean NOT NULL DEFAULT false,
    sequence_no integer NOT NULL,
    note text NULL,
    CONSTRAINT ck_process_template_stage_machines_sequence_positive CHECK (sequence_no > 0),
    CONSTRAINT ux_process_template_stage_machines_equipment UNIQUE (manufacturing_process_template_stage_id, equipment_id),
    CONSTRAINT ux_process_template_stage_machines_sequence UNIQUE (manufacturing_process_template_stage_id, sequence_no)
);

CREATE TABLE IF NOT EXISTS bom.manufacturing_bom_stage_checklist_items (
    manufacturing_bom_stage_checklist_item_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    manufacturing_bom_stage_work_instruction_id uuid NOT NULL REFERENCES bom.manufacturing_bom_stage_work_instructions(manufacturing_bom_stage_work_instruction_id) ON DELETE CASCADE,
    source_checklist_item_id uuid NULL REFERENCES bom.manufacturing_work_instruction_checklist_items(manufacturing_work_instruction_checklist_item_id) ON DELETE SET NULL,
    external_id_snapshot varchar(64) NOT NULL,
    content_snapshot text NOT NULL,
    sequence_no integer NOT NULL,
    is_required boolean NOT NULL,
    expected_value_snapshot varchar(200) NULL,
    unit_snapshot varchar(32) NULL,
    CONSTRAINT ck_bom_stage_checklist_sequence_positive CHECK (sequence_no > 0),
    CONSTRAINT ux_bom_stage_checklist_external_id UNIQUE (manufacturing_bom_stage_work_instruction_id, external_id_snapshot),
    CONSTRAINT ux_bom_stage_checklist_sequence UNIQUE (manufacturing_bom_stage_work_instruction_id, sequence_no)
);
