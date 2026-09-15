CREATE TABLE IF NOT EXISTS printect."PrintLabelTemplates" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "companyId" uuid NOT NULL,
    code citext NOT NULL,
    name varchar(200) NOT NULL,
    "labelType" citext NULL,
    instructions text NULL,
    "widthMm" numeric(10,2) NOT NULL,
    "heightMm" numeric(10,2) NOT NULL,
    "attachmentCollectionId" uuid NULL REFERENCES "Attachment"."AttachmentCollection"("AttachmentCollectionID") ON DELETE SET NULL,
    "isActive" boolean NOT NULL DEFAULT true,
    "createdBy" uuid NULL,
    "createdDate" timestamp without time zone NOT NULL,
    "updatedBy" uuid NULL,
    "updatedDate" timestamp without time zone NULL,
    CONSTRAINT "CK_PrintLabelTemplates_Dimensions" CHECK ("widthMm" > 0 AND "heightMm" > 0),
    CONSTRAINT "UX_PrintLabelTemplates_Company_Code" UNIQUE ("companyId", code)
);

CREATE TABLE IF NOT EXISTS printect."PrintLabelLogos" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "companyId" uuid NOT NULL,
    code citext NOT NULL,
    name varchar(200) NOT NULL,
    "attachmentCollectionId" uuid NOT NULL REFERENCES "Attachment"."AttachmentCollection"("AttachmentCollectionID") ON DELETE RESTRICT,
    "isActive" boolean NOT NULL DEFAULT true,
    "createdBy" uuid NULL,
    "createdDate" timestamp without time zone NOT NULL,
    "updatedBy" uuid NULL,
    "updatedDate" timestamp without time zone NULL,
    CONSTRAINT "UX_PrintLabelLogos_Company_Code" UNIQUE ("companyId", code)
);

CREATE TABLE IF NOT EXISTS printect."PrintLabelElements" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "printLabelTemplateId" uuid NOT NULL REFERENCES printect."PrintLabelTemplates"(id) ON DELETE CASCADE,
    "lineNo" integer NOT NULL,
    "fieldKey" citext NOT NULL,
    "defaultValue" text NULL,
    "isActive" boolean NOT NULL DEFAULT true,
    CONSTRAINT "CK_PrintLabelElements_LineNo" CHECK ("lineNo" > 0),
    CONSTRAINT "UX_PrintLabelElements_Template_Line" UNIQUE ("printLabelTemplateId", "lineNo")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_PrintLabelElements_Template_Field"
    ON printect."PrintLabelElements"("printLabelTemplateId", "fieldKey");

CREATE TABLE IF NOT EXISTS printect."PrintLabelTemplateLogos" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "printLabelTemplateId" uuid NOT NULL REFERENCES printect."PrintLabelTemplates"(id) ON DELETE CASCADE,
    "printLabelLogoId" uuid NOT NULL REFERENCES printect."PrintLabelLogos"(id) ON DELETE RESTRICT,
    "sortOrder" integer NOT NULL DEFAULT 0,
    "isDefault" boolean NOT NULL DEFAULT false,
    "isActive" boolean NOT NULL DEFAULT true,
    CONSTRAINT "UX_PrintLabelTemplateLogos_Template_Logo" UNIQUE ("printLabelTemplateId", "printLabelLogoId")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_PrintLabelTemplateLogos_OneDefault"
    ON printect."PrintLabelTemplateLogos"("printLabelTemplateId")
    WHERE "isDefault" = true AND "isActive" = true;

ALTER TABLE printect."CustomerLabelHeaders"
    ADD COLUMN IF NOT EXISTS "printLabelTemplateId" uuid NULL,
    ADD COLUMN IF NOT EXISTS "defaultPrintLabelLogoId" uuid NULL;

ALTER TABLE printect."CustomerLabelDetails"
    ADD COLUMN IF NOT EXISTS "printLabelElementId" uuid NULL;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CustomerLabelHeaders_PrintLabelTemplate') THEN
        ALTER TABLE printect."CustomerLabelHeaders"
            ADD CONSTRAINT "FK_CustomerLabelHeaders_PrintLabelTemplate"
            FOREIGN KEY ("printLabelTemplateId") REFERENCES printect."PrintLabelTemplates"(id) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CustomerLabelHeaders_DefaultPrintLabelLogo') THEN
        ALTER TABLE printect."CustomerLabelHeaders"
            ADD CONSTRAINT "FK_CustomerLabelHeaders_DefaultPrintLabelLogo"
            FOREIGN KEY ("defaultPrintLabelLogoId") REFERENCES printect."PrintLabelLogos"(id) ON DELETE SET NULL;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CustomerLabelDetails_PrintLabelElement') THEN
        ALTER TABLE printect."CustomerLabelDetails"
            ADD CONSTRAINT "FK_CustomerLabelDetails_PrintLabelElement"
            FOREIGN KEY ("printLabelElementId") REFERENCES printect."PrintLabelElements"(id) ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_CustomerLabelHeaders_PrintLabelTemplateId"
    ON printect."CustomerLabelHeaders"("printLabelTemplateId");
CREATE INDEX IF NOT EXISTS "IX_CustomerLabelHeaders_DefaultPrintLabelLogoId"
    ON printect."CustomerLabelHeaders"("defaultPrintLabelLogoId");
CREATE INDEX IF NOT EXISTS "IX_CustomerLabelDetails_PrintLabelElementId"
    ON printect."CustomerLabelDetails"("printLabelElementId");
