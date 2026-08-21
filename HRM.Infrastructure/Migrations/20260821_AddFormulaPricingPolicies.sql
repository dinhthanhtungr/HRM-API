BEGIN;

CREATE TABLE "Customer"."FormulaPricingPolicies"
(
    "FormulaPricingPolicyId" uuid NOT NULL DEFAULT gen_random_uuid(),
    "CompanyId" uuid NOT NULL,
    "Profile" integer NOT NULL,
    "Currency" citext NOT NULL DEFAULT 'VND',
    "Name" citext NOT NULL,
    "Version" integer NOT NULL DEFAULT 1,
    "DefaultManufacturingCost" numeric(22, 6) NOT NULL,
    "Status" integer NOT NULL DEFAULT 0,
    "EffectiveFrom" timestamp without time zone NULL,
    "PublishedBy" uuid NULL,
    "PublishedAt" timestamp without time zone NULL,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedBy" uuid NOT NULL,
    "CreatedDate" timestamp without time zone NOT NULL,
    "UpdatedBy" uuid NULL,
    "UpdatedDate" timestamp without time zone NULL,
    CONSTRAINT "PK_FormulaPricingPolicies" PRIMARY KEY ("FormulaPricingPolicyId"),
    CONSTRAINT "FK_FormulaPricingPolicies_Company" FOREIGN KEY ("CompanyId")
        REFERENCES "company"."Companies" ("CompanyId") ON DELETE RESTRICT,
    CONSTRAINT "FK_FormulaPricingPolicies_CreatedBy" FOREIGN KEY ("CreatedBy")
        REFERENCES "hr"."Employees" ("EmployeeId") ON DELETE RESTRICT,
    CONSTRAINT "FK_FormulaPricingPolicies_UpdatedBy" FOREIGN KEY ("UpdatedBy")
        REFERENCES "hr"."Employees" ("EmployeeId") ON DELETE SET NULL,
    CONSTRAINT "FK_FormulaPricingPolicies_PublishedBy" FOREIGN KEY ("PublishedBy")
        REFERENCES "hr"."Employees" ("EmployeeId") ON DELETE SET NULL
);

CREATE UNIQUE INDEX "UX_FormulaPricingPolicies_Company_Profile_Currency_Version"
    ON "Customer"."FormulaPricingPolicies" ("CompanyId", "Profile", "Currency", "Version");

CREATE INDEX "IX_FormulaPricingPolicies_Current"
    ON "Customer"."FormulaPricingPolicies" ("CompanyId", "Profile", "Currency", "Status", "IsActive");

CREATE TABLE "Customer"."FormulaPricingPolicyTiers"
(
    "FormulaPricingPolicyTierId" uuid NOT NULL DEFAULT gen_random_uuid(),
    "FormulaPricingPolicyId" uuid NOT NULL,
    "QuantityRangeLabel" citext NOT NULL,
    "MinQuantity" numeric(18, 3) NULL,
    "MaxQuantity" numeric(18, 3) NULL,
    "MinInclusive" boolean NOT NULL DEFAULT TRUE,
    "MaxInclusive" boolean NOT NULL DEFAULT TRUE,
    "PriceOffset" numeric(22, 6) NULL,
    "SortOrder" integer NOT NULL DEFAULT 0,
    CONSTRAINT "PK_FormulaPricingPolicyTiers" PRIMARY KEY ("FormulaPricingPolicyTierId"),
    CONSTRAINT "FK_FormulaPricingPolicyTiers_Policy" FOREIGN KEY ("FormulaPricingPolicyId")
        REFERENCES "Customer"."FormulaPricingPolicies" ("FormulaPricingPolicyId") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "UX_FormulaPricingPolicyTiers_Policy_SortOrder"
    ON "Customer"."FormulaPricingPolicyTiers" ("FormulaPricingPolicyId", "SortOrder");

ALTER TABLE "Customer"."ProductPricingVersions"
    ADD COLUMN "FormulaPricingPolicyId" uuid NULL,
    ADD COLUMN "HasManualTierAdjustment" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Customer"."ProductPricingVersions"
    ADD CONSTRAINT "FK_ProductPricingVersions_FormulaPricingPolicy"
        FOREIGN KEY ("FormulaPricingPolicyId")
        REFERENCES "Customer"."FormulaPricingPolicies" ("FormulaPricingPolicyId")
        ON DELETE RESTRICT;

CREATE INDEX "IX_ProductPricingVersions_FormulaPricingPolicyId"
    ON "Customer"."ProductPricingVersions" ("FormulaPricingPolicyId");

COMMIT;
