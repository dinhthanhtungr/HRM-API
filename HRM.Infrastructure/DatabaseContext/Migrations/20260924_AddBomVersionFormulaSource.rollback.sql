DROP INDEX IF EXISTS bom.ix_bom_versions_source_formula;

ALTER TABLE bom.bom_versions
    DROP CONSTRAINT IF EXISTS ck_bom_versions_single_source,
    DROP CONSTRAINT IF EXISTS fk_bom_versions_source_formula,
    DROP COLUMN IF EXISTS source_formula_id;
