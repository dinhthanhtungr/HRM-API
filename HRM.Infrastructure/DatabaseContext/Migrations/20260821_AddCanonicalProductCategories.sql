-- Adds the six canonical product categories used by the Sample Request form.
-- Legacy categories are preserved so existing products, formulas, warehouse data,
-- and historical documents keep their current foreign-key references.

INSERT INTO "Material"."Categories"
    ("CategoryId", "ExternalId", "Types", "Name", "CompanyId", "IsActive")
SELECT
    gen_random_uuid(),
    definitions."ExternalId",
    'Product',
    definitions."Name",
    companies."CompanyId",
    TRUE
FROM
(
    SELECT DISTINCT "CompanyId"
    FROM "Material"."Categories"
    WHERE "Types" = 'Product'
) AS companies
CROSS JOIN
(
    VALUES
        ('CMP', 'Compound'),
        ('CMB', 'Color masterbatch'),
        ('AMB', 'Additive masterbatch'),
        ('PIG', 'Bột màu'),
        ('VRG', 'Hạt nhựa nguyên sinh'),
        ('ADD', 'Phụ gia')
) AS definitions("ExternalId", "Name")
WHERE NOT EXISTS
(
    SELECT 1
    FROM "Material"."Categories" existing
    WHERE existing."CompanyId" = companies."CompanyId"
      AND existing."Types" = 'Product'
      AND existing."ExternalId" = definitions."ExternalId"
);
