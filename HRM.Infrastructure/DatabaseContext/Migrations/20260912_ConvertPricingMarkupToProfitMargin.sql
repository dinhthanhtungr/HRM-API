-- Converts persisted pricing percentages from markup-on-cost to profit margin-on-revenue.
--
-- Old: (selling price - cost base) / cost base * 100
-- New: (selling price - cost base) / selling price * 100
-- Conversion: new margin = old markup / (100 + old markup) * 100
--
-- The check constraints are both domain guards and idempotency markers. If this script is
-- executed again, a table whose marker already exists is not converted a second time.

BEGIN;

DO $migration$
BEGIN
    IF to_regclass('"Customer"."ProductPricingVersions"') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'Customer'
             AND table_name = 'ProductPricingVersions'
             AND column_name = 'ProfitMarginRate')
       AND NOT EXISTS (
           SELECT 1
           FROM pg_constraint
           WHERE conname = 'CK_ProductPricingVersions_ProfitMarginRate_ProfitMargin')
    THEN
        UPDATE "Customer"."ProductPricingVersions"
        SET "ProfitMarginRate" = round(
            "ProfitMarginRate" / (100 + "ProfitMarginRate") * 100,
            4)
        WHERE "ProfitMarginRate" IS NOT NULL;

        ALTER TABLE "Customer"."ProductPricingVersions"
            ADD CONSTRAINT "CK_ProductPricingVersions_ProfitMarginRate_ProfitMargin"
            CHECK ("ProfitMarginRate" IS NULL OR
                   ("ProfitMarginRate" >= 0 AND "ProfitMarginRate" < 100));
    END IF;
END
$migration$;

DO $migration$
BEGIN
    IF to_regclass('"Customer"."FormulaPricingPolicies"') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'Customer'
             AND table_name = 'FormulaPricingPolicies'
             AND column_name = 'DefaultProfitMarginRate')
       AND NOT EXISTS (
           SELECT 1
           FROM pg_constraint
           WHERE conname = 'CK_FormulaPricingPolicies_DefaultProfitMarginRate_ProfitMargin')
    THEN
        UPDATE "Customer"."FormulaPricingPolicies"
        SET "DefaultProfitMarginRate" = round(
            "DefaultProfitMarginRate" / (100 + "DefaultProfitMarginRate") * 100,
            4);

        ALTER TABLE "Customer"."FormulaPricingPolicies"
            ADD CONSTRAINT "CK_FormulaPricingPolicies_DefaultProfitMarginRate_ProfitMargin"
            CHECK ("DefaultProfitMarginRate" >= 0 AND "DefaultProfitMarginRate" < 100);
    END IF;
END
$migration$;

COMMIT;
