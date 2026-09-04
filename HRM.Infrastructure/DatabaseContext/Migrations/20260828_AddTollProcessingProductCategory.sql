-- Adds the canonical Gia công category for every company that already uses Product categories.
-- Safe to run repeatedly; legacy category rows and existing foreign keys are not changed.

INSERT INTO "Material"."Categories"
    ("CategoryId", "ExternalId", "Types", "Name", "CompanyId", "IsActive")
SELECT
    gen_random_uuid(),
    'GCO',
    'Product',
    'Gia công',
    companies."CompanyId",
    TRUE
FROM
(
    SELECT DISTINCT "CompanyId"
    FROM "Material"."Categories"
    WHERE "Types" = 'Product'
) AS companies
WHERE NOT EXISTS
(
    SELECT 1
    FROM "Material"."Categories" existing
    WHERE existing."CompanyId" = companies."CompanyId"
      AND existing."Types" = 'Product'
      AND existing."ExternalId" = 'GCO'
);
