-- Creates the row-per-trial source used by the Sample Request Lab report API.
-- This repository does not currently keep an EF Core migrations history, so the
-- deployment artifact is a targeted, idempotent PostgreSQL script.

CREATE SCHEMA IF NOT EXISTS "SampleRequests";

CREATE TABLE IF NOT EXISTS "SampleRequests"."SampleRequestSampleTrials"
(
    "SampleRequestSampleTrialId" uuid NOT NULL DEFAULT gen_random_uuid(),
    "SampleRequestId" uuid NOT NULL,
    "FormulaId" uuid NULL,
    "TrialNo" integer NOT NULL,
    "Status" character varying(50) NOT NULL DEFAULT 'Draft',

    "CustomerNameSnapshot" character varying(500) NULL,
    "SampleRequestExternalIdSnapshot" character varying(100) NULL,
    "ProductNameSnapshot" character varying(1000) NULL,
    "ColourCodeSnapshot" character varying(100) NULL,
    "CategoryNameSnapshot" character varying(255) NULL,

    "BatchNo" character varying(100) NULL,
    "DeliveredSampleQuantityKg" numeric(18,4) NULL,
    "AdditiveRate" numeric(18,4) NULL,
    "RequestReceivedDate" timestamp without time zone NULL,
    "FinishedDate" timestamp without time zone NULL,
    "SentDate" timestamp without time zone NULL,
    "DeliveryMethod" character varying(100) NULL,
    "LabNote" text NULL,
    "SentByEmployeeId" uuid NULL,

    "CustomerReplyStatus" character varying(50) NULL,
    "CustomerReplyDate" timestamp without time zone NULL,
    "CustomerReplyByEmployeeId" uuid NULL,
    "CustomerReplyNote" text NULL,
    "OrderDate" timestamp without time zone NULL,

    "CreatedBy" uuid NULL,
    "CreatedDate" timestamp without time zone NOT NULL,
    "UpdatedBy" uuid NULL,
    "UpdatedDate" timestamp without time zone NULL,
    "IsActive" boolean NOT NULL DEFAULT TRUE,

    CONSTRAINT "PK_SampleRequestSampleTrials"
        PRIMARY KEY ("SampleRequestSampleTrialId"),
    CONSTRAINT "FK_SampleRequestSampleTrials_SampleRequest"
        FOREIGN KEY ("SampleRequestId")
        REFERENCES "SampleRequests"."SampleRequests" ("SampleRequestId")
        ON DELETE RESTRICT,
    CONSTRAINT "FK_SampleRequestSampleTrials_Formula"
        FOREIGN KEY ("FormulaId")
        REFERENCES "SampleRequests"."Formulas" ("FormulaId")
        ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_SampleRequestSampleTrials_Request_TrialNo"
    ON "SampleRequests"."SampleRequestSampleTrials" ("SampleRequestId", "TrialNo");

CREATE INDEX IF NOT EXISTS "IX_SampleRequestSampleTrials_Request_Active"
    ON "SampleRequests"."SampleRequestSampleTrials" ("SampleRequestId", "IsActive");

CREATE INDEX IF NOT EXISTS "IX_SampleRequestSampleTrials_Status_ReplyStatus"
    ON "SampleRequests"."SampleRequestSampleTrials" ("Status", "CustomerReplyStatus");

CREATE INDEX IF NOT EXISTS "IX_SampleRequestSampleTrials_SentDate"
    ON "SampleRequests"."SampleRequestSampleTrials" ("SentDate");

CREATE INDEX IF NOT EXISTS "IX_SampleRequestSampleTrials_CustomerReplyDate"
    ON "SampleRequests"."SampleRequestSampleTrials" ("CustomerReplyDate");
