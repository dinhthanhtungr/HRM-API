-- Seeds the canonical Product categories with the stable CategoryId values from TestEnvironment.
-- Safe to run repeatedly. It never updates or deletes existing categories or Products.
--
-- Target company resolution:
--   1. Use the TestEnvironment CompanyId when it exists in the target database.
--   2. Otherwise use the single active company, if exactly one exists.
--   3. Fail without changing data when the target has zero or multiple eligible companies.
--
-- If the target already has a Product category with the same ExternalId, it is preserved,
-- even when its CategoryId differs. Do not rewrite an existing CategoryId because it may be
-- referenced by Products, Formulas, warehouse data, and historical documents.

DO
$$
DECLARE
    target_company_id uuid;
    active_company_count integer;
BEGIN
    SELECT "companyId"
    INTO target_company_id
    FROM "company"."Companies"
    WHERE "companyId" = 'f54b3c96-4faa-43d1-8446-9d98c459c630';

    IF target_company_id IS NULL THEN
        SELECT COUNT(*)
        INTO active_company_count
        FROM "company"."Companies"
        WHERE "isActive" = TRUE;

        IF active_company_count <> 1 THEN
            RAISE EXCEPTION
                'Cannot resolve target company. Expected TestEnvironment company or exactly one active company; found % active companies.',
                active_company_count;
        END IF;

        SELECT "companyId"
        INTO target_company_id
        FROM "company"."Companies"
        WHERE "isActive" = TRUE;
    END IF;

    INSERT INTO "Material"."Categories"
        ("CategoryId", "ExternalId", "Types", "Name", "CompanyId", "IsActive")
    SELECT
        seed."CategoryId",
        seed."ExternalId",
        'Product',
        seed."Name",
        target_company_id,
        TRUE
    FROM
    (
        VALUES
            ('176b7009-fe95-4d05-aee7-e14ff165addf'::uuid, 'ADD', 'Phụ gia'),
            ('a1837c90-e728-4c56-9661-0c515663b215'::uuid, 'AMB', 'Additive masterbatch'),
            ('083f35e0-3927-456c-bb2f-195bc3731e1d'::uuid, 'CMB', 'Color masterbatch'),
            ('b9dac809-e661-4893-b2fe-4d6942767ac1'::uuid, 'CMP', 'Compound'),
            ('a6ee93d1-a9c4-488d-a0de-4131476f0ec6'::uuid, 'GCO', 'Gia công'),
            ('7f208f68-f09b-444c-b38a-46b8132ddb1b'::uuid, 'PIG', 'Bột màu'),
            ('9b02a6c0-6572-48d4-8397-0f9d4b174412'::uuid, 'VRG', 'Hạt nhựa nguyên sinh')
    ) AS seed("CategoryId", "ExternalId", "Name")
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM "Material"."Categories" existing
        WHERE existing."CompanyId" = target_company_id
          AND existing."Types" = 'Product'
          AND existing."ExternalId" = seed."ExternalId"
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM "Material"."Categories" existing
        WHERE existing."CategoryId" = seed."CategoryId"
    );
END
$$;

-- Verification: each canonical code should appear once for the resolved target company.
SELECT
    "ExternalId" AS "Code",
    "CategoryId",
    "Name",
    "CompanyId",
    "IsActive"
FROM "Material"."Categories"
WHERE "Types" = 'Product'
  AND "ExternalId" IN ('CMP', 'CMB', 'AMB', 'PIG', 'VRG', 'ADD', 'GCO')
ORDER BY "ExternalId";
