-- Emergency rollback for 20260912_ConvertPricingMarkupToProfitMargin.sql.
-- Restores markup semantics approximately; values were rounded to four decimals by the forward migration.

BEGIN;

ALTER TABLE IF EXISTS "Customer"."ProductPricingVersions"
    DROP CONSTRAINT IF EXISTS "CK_ProductPricingVersions_ProfitMarginRate_ProfitMargin";

UPDATE "Customer"."ProductPricingVersions"
SET "ProfitMarginRate" = round(
    "ProfitMarginRate" / (100 - "ProfitMarginRate") * 100,
    4)
WHERE "ProfitMarginRate" IS NOT NULL;

ALTER TABLE IF EXISTS "Customer"."FormulaPricingPolicies"
    DROP CONSTRAINT IF EXISTS "CK_FormulaPricingPolicies_DefaultProfitMarginRate_ProfitMargin";

UPDATE "Customer"."FormulaPricingPolicies"
SET "DefaultProfitMarginRate" = round(
    "DefaultProfitMarginRate" / (100 - "DefaultProfitMarginRate") * 100,
    4);

COMMIT;
