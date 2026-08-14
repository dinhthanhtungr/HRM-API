-- Adds immutable business snapshots for development formulas.
-- This repository keeps targeted PostgreSQL deployment scripts instead of an EF ModelSnapshot.

CREATE SCHEMA IF NOT EXISTS "SampleRequests";

CREATE TABLE IF NOT EXISTS "SampleRequests"."FormulaVersions"
(
    "FormulaVersionId" uuid NOT NULL,
    "FormulaId" uuid NOT NULL,
    "VersionNo" integer NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Status" character varying(32) NOT NULL DEFAULT 'Draft',
    "Note" text NULL,
    "TotalPrice" numeric(22,6) NOT NULL,
    "ProductionPrice" numeric(22,6) NULL,
    "PresidentPrice" numeric(22,6) NULL,
    "ProfitMarginPrice" numeric(22,6) NULL,
    "EffectiveFrom" timestamp without time zone NULL,
    "EffectiveTo" timestamp without time zone NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "CreatedBy" uuid NULL,
    "ChangeReason" character varying(500) NULL,

    CONSTRAINT "PK_FormulaVersions"
        PRIMARY KEY ("FormulaVersionId"),
    CONSTRAINT "FK_FormulaVersions_Formulas"
        FOREIGN KEY ("FormulaId")
        REFERENCES "SampleRequests"."Formulas" ("FormulaId")
        ON DELETE CASCADE,
    CONSTRAINT "FK_FormulaVersions_Employees_CreatedBy"
        FOREIGN KEY ("CreatedBy")
        REFERENCES "hr"."Employees" ("EmployeeId")
        ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS "SampleRequests"."FormulaVersionItems"
(
    "FormulaVersionItemId" uuid NOT NULL,
    "FormulaVersionId" uuid NOT NULL,
    "LineNo" integer NOT NULL,
    "ItemType" integer NOT NULL,
    "MaterialId" uuid NULL,
    "ProductId" uuid NULL,
    "CategoryId" uuid NOT NULL,
    "Quantity" numeric(12,10) NOT NULL,
    "UnitPrice" numeric(22,6) NOT NULL,
    "TotalPrice" numeric(22,6) NOT NULL,
    "Unit" character varying(50) NULL,
    "MaterialExternalIdSnapshot" character varying(100) NULL,
    "MaterialNameSnapshot" character varying(500) NULL,

    CONSTRAINT "PK_FormulaVersionItems"
        PRIMARY KEY ("FormulaVersionItemId"),
    CONSTRAINT "FK_FormulaVersionItems_FormulaVersions"
        FOREIGN KEY ("FormulaVersionId")
        REFERENCES "SampleRequests"."FormulaVersions" ("FormulaVersionId")
        ON DELETE CASCADE,
    CONSTRAINT "FK_FormulaVersionItems_Materials"
        FOREIGN KEY ("MaterialId")
        REFERENCES "Material"."Materials" ("MaterialId")
        ON DELETE RESTRICT,
    CONSTRAINT "FK_FormulaVersionItems_Products"
        FOREIGN KEY ("ProductId")
        REFERENCES "SampleRequests"."Products" ("ProductId")
        ON DELETE RESTRICT,
    CONSTRAINT "FK_FormulaVersionItems_Categories"
        FOREIGN KEY ("CategoryId")
        REFERENCES "Material"."Categories" ("CategoryId")
        ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_FormulaVersions_FormulaId_VersionNo"
    ON "SampleRequests"."FormulaVersions" ("FormulaId", "VersionNo");

CREATE INDEX IF NOT EXISTS "IX_FormulaVersions_FormulaId_Period"
    ON "SampleRequests"."FormulaVersions" ("FormulaId", "EffectiveFrom", "EffectiveTo");

CREATE INDEX IF NOT EXISTS "IX_FormulaVersions_CreatedBy"
    ON "SampleRequests"."FormulaVersions" ("CreatedBy");

CREATE UNIQUE INDEX IF NOT EXISTS "UX_FormulaVersionItems_VersionId_LineNo"
    ON "SampleRequests"."FormulaVersionItems" ("FormulaVersionId", "LineNo");

CREATE INDEX IF NOT EXISTS "IX_FormulaVersionItems_MaterialId"
    ON "SampleRequests"."FormulaVersionItems" ("MaterialId");

CREATE INDEX IF NOT EXISTS "IX_FormulaVersionItems_ProductId"
    ON "SampleRequests"."FormulaVersionItems" ("ProductId");
