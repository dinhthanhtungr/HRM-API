-- Keep ManufacturingStageTransitionType storage consistent across process templates and M-BOM snapshots.
-- Enum values: 1=ProcessFlow, 2=MachineChange, 3=PositionChange, 4=ColorChange, 5=Cleaning.

DO $$
BEGIN
    IF to_regclass('bom.manufacturing_bom_stage_transitions') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'bom'
             AND table_name = 'manufacturing_bom_stage_transitions'
             AND column_name = 'transition_type'
             AND udt_name <> 'int4'
       ) THEN
        IF EXISTS (
            SELECT 1
            FROM bom.manufacturing_bom_stage_transitions
            WHERE lower(transition_type::text) NOT IN (
                'processflow', 'machinechange', 'positionchange',
                'colorchange', 'cleaning', '1', '2', '3', '4', '5'
            )
        ) THEN
            RAISE EXCEPTION 'bom.manufacturing_bom_stage_transitions contains an unsupported transition_type';
        END IF;

        ALTER TABLE bom.manufacturing_bom_stage_transitions
            ALTER COLUMN transition_type TYPE integer
            USING CASE lower(transition_type::text)
                WHEN 'processflow' THEN 1
                WHEN 'machinechange' THEN 2
                WHEN 'positionchange' THEN 3
                WHEN 'colorchange' THEN 4
                WHEN 'cleaning' THEN 5
                WHEN '1' THEN 1
                WHEN '2' THEN 2
                WHEN '3' THEN 3
                WHEN '4' THEN 4
                WHEN '5' THEN 5
            END;
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('bom.manufacturing_process_template_stage_transitions') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'bom'
             AND table_name = 'manufacturing_process_template_stage_transitions'
             AND column_name = 'transition_type'
             AND udt_name <> 'int4'
       ) THEN
        IF EXISTS (
            SELECT 1
            FROM bom.manufacturing_process_template_stage_transitions
            WHERE lower(transition_type::text) NOT IN (
                'processflow', 'machinechange', 'positionchange',
                'colorchange', 'cleaning', '1', '2', '3', '4', '5'
            )
        ) THEN
            RAISE EXCEPTION 'bom.manufacturing_process_template_stage_transitions contains an unsupported transition_type';
        END IF;

        ALTER TABLE bom.manufacturing_process_template_stage_transitions
            ALTER COLUMN transition_type TYPE integer
            USING CASE lower(transition_type::text)
                WHEN 'processflow' THEN 1
                WHEN 'machinechange' THEN 2
                WHEN 'positionchange' THEN 3
                WHEN 'colorchange' THEN 4
                WHEN 'cleaning' THEN 5
                WHEN '1' THEN 1
                WHEN '2' THEN 2
                WHEN '3' THEN 3
                WHEN '4' THEN 4
                WHEN '5' THEN 5
            END;
    END IF;
END $$;

ALTER TABLE IF EXISTS bom.manufacturing_bom_stage_transitions
    DROP CONSTRAINT IF EXISTS ck_manufacturing_bom_stage_transitions_type_valid;
ALTER TABLE IF EXISTS bom.manufacturing_bom_stage_transitions
    ADD CONSTRAINT ck_manufacturing_bom_stage_transitions_type_valid
    CHECK (transition_type BETWEEN 1 AND 5);

ALTER TABLE IF EXISTS bom.manufacturing_process_template_stage_transitions
    DROP CONSTRAINT IF EXISTS ck_process_template_transitions_type_valid;
ALTER TABLE IF EXISTS bom.manufacturing_process_template_stage_transitions
    ADD CONSTRAINT ck_process_template_transitions_type_valid
    CHECK (transition_type BETWEEN 1 AND 5);
