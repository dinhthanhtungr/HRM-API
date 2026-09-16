-- Preserve the Formula/MfgFormula quantity scale when an M-BOM is initialized from Formula.
-- Safe for the current empty BOM dataset; numeric(12,10) aligns with FormulaMaterials and ManufacturingFormulaMaterials.
ALTER TABLE bom.bom_version_items
    ALTER COLUMN quantity TYPE numeric(12, 10)
    USING quantity::numeric(12, 10);
