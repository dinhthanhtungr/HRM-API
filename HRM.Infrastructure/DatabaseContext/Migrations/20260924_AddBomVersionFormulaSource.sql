-- Track the immutable Lab Formula that initialized an Engineering BOM version.
ALTER TABLE bom.bom_versions
    ADD COLUMN IF NOT EXISTS source_formula_id uuid NULL;

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_bom_versions_source_formula'
    ) THEN
        ALTER TABLE bom.bom_versions
            ADD CONSTRAINT fk_bom_versions_source_formula
            FOREIGN KEY (source_formula_id)
            REFERENCES "SampleRequests"."Formulas"("FormulaId")
            ON DELETE RESTRICT;
    END IF;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_bom_versions_single_source'
    ) THEN
        ALTER TABLE bom.bom_versions
            ADD CONSTRAINT ck_bom_versions_single_source
            CHECK (source_formula_id IS NULL OR source_engineering_bom_version_id IS NULL);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_bom_versions_source_formula
    ON bom.bom_versions(source_formula_id);
