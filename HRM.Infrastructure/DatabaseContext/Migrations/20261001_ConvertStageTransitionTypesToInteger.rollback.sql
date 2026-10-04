ALTER TABLE IF EXISTS bom.manufacturing_bom_stage_transitions
    DROP CONSTRAINT IF EXISTS ck_manufacturing_bom_stage_transitions_type_valid;

ALTER TABLE IF EXISTS bom.manufacturing_process_template_stage_transitions
    DROP CONSTRAINT IF EXISTS ck_process_template_transitions_type_valid;

DO $$
BEGIN
    IF to_regclass('bom.manufacturing_bom_stage_transitions') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'bom'
             AND table_name = 'manufacturing_bom_stage_transitions'
             AND column_name = 'transition_type'
             AND udt_name = 'int4'
       ) THEN
        ALTER TABLE bom.manufacturing_bom_stage_transitions
            ALTER COLUMN transition_type TYPE citext
            USING CASE transition_type
                WHEN 1 THEN 'ProcessFlow'
                WHEN 2 THEN 'MachineChange'
                WHEN 3 THEN 'PositionChange'
                WHEN 4 THEN 'ColorChange'
                WHEN 5 THEN 'Cleaning'
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
             AND udt_name = 'int4'
       ) THEN
        ALTER TABLE bom.manufacturing_process_template_stage_transitions
            ALTER COLUMN transition_type TYPE citext
            USING CASE transition_type
                WHEN 1 THEN 'ProcessFlow'
                WHEN 2 THEN 'MachineChange'
                WHEN 3 THEN 'PositionChange'
                WHEN 4 THEN 'ColorChange'
                WHEN 5 THEN 'Cleaning'
            END;
    END IF;
END $$;
