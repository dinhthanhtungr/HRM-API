-- PHASE 2 MANUAL DEPLOYMENT SCRIPT. Not an auto-discovered EF migration.
-- Tested on an isolated synthetic PostgreSQL cluster, NEVER applied to the API database by this task.
-- Requires approved clone rehearsal, verified target, backup and paused ALL writers/workers.
-- Use an error-stopping SQL client. Default guard fails before any DDL or business-data writes.
-- An operator may set the session GUC hrm.group_storage_review_approved = 'yes' ONLY after approval.
-- One-time transactional expansion: an existing mapping table is rejected, never overwritten.
BEGIN;
SET LOCAL lock_timeout = '3s';
SET LOCAL statement_timeout = '120s';

DO $approval$
BEGIN
  IF current_setting('hrm.group_storage_review_approved', true) IS DISTINCT FROM 'yes' THEN
    RAISE EXCEPTION 'Review-only draft: explicit deployment approval is required';
  END IF;
  IF to_regclass('"InternalMail"."InternalConversationGroups"') IS NOT NULL THEN
    RAISE EXCEPTION 'Mapping already exists; inspect deployment state, do not rerun expansion';
  END IF;
END
$approval$;

-- Freeze the baseline. Never run during normal traffic; lock timeout fails safely if writers remain.
LOCK TABLE "InternalMail"."InternalConversations" IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE "InternalMail"."InternalConversationParticipants",
           "InternalMail"."InternalMessages", "InternalMail"."InternalMessageReadStates",
           "InternalMail"."InternalMessageReferences", "InternalMail"."InternalMessageAttachments",
           notification.notifications, notification.notification_user_states,
           notification.notification_recipients, notification.outbox_messages IN SHARE MODE;

DO $preflight$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_class WHERE oid = '"InternalMail"."InternalConversations"'::regclass AND relrowsecurity) THEN
    RAISE EXCEPTION 'Existing conversation RLS requires explicit policy design for the new table';
  END IF;
  IF EXISTS (
    SELECT 1 FROM "InternalMail"."InternalConversations" c
    LEFT JOIN "InternalMail"."InternalConversations" p ON p."InternalConversationId" = c."RelatedId"
    WHERE c."RelatedType" IN (17,18,19) AND (
      p."InternalConversationId" IS NULL OR p."CompanyId" <> c."CompanyId" OR
      c."InternalConversationId" = c."RelatedId" OR p."RelatedType" IN (17,18,19) OR
      (c."IsActive" AND NOT p."IsActive"))) THEN
    RAISE EXCEPTION 'Invalid parent/company/lifecycle: resolve explicitly before migration';
  END IF;
  IF EXISTS (
    SELECT 1 FROM "InternalMail"."InternalConversations"
    WHERE "RelatedType" IN (17,18) AND "IsActive"
    GROUP BY "CompanyId", "RelatedId", "RelatedType" HAVING count(*) > 1) THEN
    RAISE EXCEPTION 'Duplicate active default groups: no automatic merge';
  END IF;
  IF EXISTS (SELECT 1 FROM "InternalMail"."InternalConversations"
      WHERE "RelatedType" IS NOT NULL AND "RelatedType" NOT IN (0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,9999)) THEN
    RAISE EXCEPTION 'Unknown/reference-only conversation type';
  END IF;
  IF EXISTS (SELECT 1 FROM "InternalMail"."InternalConversations" WHERE "RelatedType" = 19 AND
      (btrim("Subject") = '' OR char_length("Subject") > 100)) THEN
    RAISE EXCEPTION 'Invalid custom group name: no truncation or silent replacement';
  END IF;
  IF EXISTS (SELECT 1 FROM "InternalMail"."InternalConversationParticipants" p
      JOIN "InternalMail"."InternalConversations" c USING ("InternalConversationId")
      JOIN hr."Employees" e ON e."EmployeeID" = p."EmployeeId" WHERE c."CompanyId" <> e."CompanyId") THEN
    RAISE EXCEPTION 'Cross-company membership requires separate review';
  END IF;
END
$preflight$;

-- Fingerprint full row multisets, including preferences, timestamps, payloads and read flags.
-- Hashes stay in a transaction-local temporary table, never emit bodies, people or identifiers.
CREATE TEMP TABLE group_storage_baseline (
  relation regclass PRIMARY KEY, row_count bigint NOT NULL, fingerprint text NOT NULL
) ON COMMIT DROP;
DO $baseline$
DECLARE relation_name regclass; row_total bigint; row_hash text;
BEGIN
  FOREACH relation_name IN ARRAY ARRAY[
    '"InternalMail"."InternalConversations"'::regclass,
    '"InternalMail"."InternalConversationParticipants"'::regclass,
    '"InternalMail"."InternalMessages"'::regclass,
    '"InternalMail"."InternalMessageReadStates"'::regclass,
    '"InternalMail"."InternalMessageReferences"'::regclass,
    '"InternalMail"."InternalMessageAttachments"'::regclass,
    'notification.notifications'::regclass, 'notification.notification_user_states'::regclass,
    'notification.notification_recipients'::regclass, 'notification.outbox_messages'::regclass
  ] LOOP
    -- regclass values are a fixed allowlist; format receives identifiers, no user SQL or payload.
    EXECUTE format('SELECT count(*), md5(coalesce(string_agg(md5(to_jsonb(t)::text), '''' ORDER BY md5(to_jsonb(t)::text)), '''')) FROM %s t', relation_name)
      INTO row_total, row_hash;
    INSERT INTO group_storage_baseline VALUES (relation_name, row_total, row_hash);
  END LOOP;
END
$baseline$;

-- A non-partial composite unique key is required by the company-scoped FK.
CREATE UNIQUE INDEX "UX_GroupStorage_Conversations_Company_Id"
  ON "InternalMail"."InternalConversations" ("CompanyId", "InternalConversationId");

CREATE TABLE "InternalMail"."InternalConversationGroups" (
  "ConversationId" uuid NOT NULL,
  "CompanyId" uuid NOT NULL,
  "RootConversationId" uuid NOT NULL,
  "GroupType" smallint NOT NULL,
  "DisplayName" varchar(100) NULL,
  "IsActive" boolean NOT NULL,
  CONSTRAINT "PK_InternalConversationGroups" PRIMARY KEY ("ConversationId"),
  CONSTRAINT "UX_InternalConversationGroups_Company_Id" UNIQUE ("CompanyId", "ConversationId"),
  CONSTRAINT "CK_InternalConversationGroups_Type" CHECK ("GroupType" IN (0,1,2,3)),
  CONSTRAINT "CK_InternalConversationGroups_Root" CHECK (
    ("GroupType" = 0 AND "RootConversationId" = "ConversationId") OR
    ("GroupType" <> 0 AND "RootConversationId" <> "ConversationId")),
  CONSTRAINT "CK_InternalConversationGroups_Name" CHECK (
    ("DisplayName" IS NULL OR btrim("DisplayName") <> '') AND
    ("GroupType" <> 3 OR "DisplayName" IS NOT NULL)),
  CONSTRAINT "FK_InternalConversationGroups_Conversation" FOREIGN KEY ("CompanyId", "ConversationId")
    REFERENCES "InternalMail"."InternalConversations" ("CompanyId", "InternalConversationId") ON DELETE RESTRICT,
  CONSTRAINT "FK_InternalConversationGroups_Root" FOREIGN KEY ("CompanyId", "RootConversationId")
    REFERENCES "InternalMail"."InternalConversationGroups" ("CompanyId", "ConversationId") ON DELETE RESTRICT
);
CREATE UNIQUE INDEX "UX_InternalConversationGroups_ActiveDefault"
  ON "InternalMail"."InternalConversationGroups" ("CompanyId", "RootConversationId", "GroupType")
  WHERE "IsActive" AND "GroupType" IN (1,2);
CREATE INDEX "IX_InternalConversationGroups_Root"
  ON "InternalMail"."InternalConversationGroups" ("CompanyId", "RootConversationId", "GroupType");

-- No duplicated creator/activity metadata. Group IsActive is an index mirror of conversation lifecycle.
CREATE FUNCTION "InternalMail"."ValidateConversationGroup"() RETURNS trigger
LANGUAGE plpgsql AS $validate$
DECLARE conversation_active boolean; root_type smallint;
BEGIN
  IF TG_OP = 'UPDATE' AND (NEW."ConversationId", NEW."CompanyId", NEW."RootConversationId", NEW."GroupType")
      IS DISTINCT FROM (OLD."ConversationId", OLD."CompanyId", OLD."RootConversationId", OLD."GroupType") THEN
    RAISE EXCEPTION 'Group identity, company, root and type are immutable';
  END IF;
  SELECT "IsActive" INTO conversation_active FROM "InternalMail"."InternalConversations"
    WHERE "InternalConversationId" = NEW."ConversationId" AND "CompanyId" = NEW."CompanyId";
  IF NOT FOUND OR NEW."IsActive" IS DISTINCT FROM conversation_active THEN
    RAISE EXCEPTION 'Group lifecycle must match its company-scoped conversation';
  END IF;
  IF NEW."GroupType" <> 0 THEN
    SELECT "GroupType" INTO root_type FROM "InternalMail"."InternalConversationGroups"
      WHERE "ConversationId" = NEW."RootConversationId" AND "CompanyId" = NEW."CompanyId";
    IF NOT FOUND OR root_type <> 0 THEN RAISE EXCEPTION 'A child must reference a General root'; END IF;
  END IF;
  RETURN NEW;
END
$validate$;
CREATE TRIGGER "TR_ValidateConversationGroup" BEFORE INSERT OR UPDATE
  ON "InternalMail"."InternalConversationGroups" FOR EACH ROW
  EXECUTE FUNCTION "InternalMail"."ValidateConversationGroup"();

-- Roots first. NULL default labels preserve FE i18n; root Subject stays the business title.
INSERT INTO "InternalMail"."InternalConversationGroups"
  ("ConversationId", "CompanyId", "RootConversationId", "GroupType", "DisplayName", "IsActive")
SELECT "InternalConversationId", "CompanyId", "InternalConversationId", 0, NULL, "IsActive"
FROM "InternalMail"."InternalConversations" WHERE coalesce("RelatedType", -1) NOT IN (17,18,19);
INSERT INTO "InternalMail"."InternalConversationGroups"
  ("ConversationId", "CompanyId", "RootConversationId", "GroupType", "DisplayName", "IsActive")
SELECT "InternalConversationId", "CompanyId", "RelatedId",
  CASE "RelatedType" WHEN 17 THEN 1 WHEN 18 THEN 2 ELSE 3 END,
  CASE WHEN "RelatedType" = 19 THEN "Subject" ELSE NULL END, "IsActive"
FROM "InternalMail"."InternalConversations" WHERE "RelatedType" IN (17,18,19);

-- Bridge for old writers during the compatibility window. No notification or member mutation.
CREATE FUNCTION "InternalMail"."SyncLegacyConversationGroup"() RETURNS trigger
LANGUAGE plpgsql AS $bridge$
DECLARE kind smallint; root_id uuid; mapped "InternalMail"."InternalConversationGroups"%ROWTYPE;
BEGIN
  IF NEW."RelatedType" IS NOT NULL AND NEW."RelatedType" NOT IN (0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,9999) THEN
    RAISE EXCEPTION 'Unsupported conversation type in legacy bridge';
  END IF;
  kind := CASE NEW."RelatedType" WHEN 17 THEN 1 WHEN 18 THEN 2 WHEN 19 THEN 3 ELSE 0 END;
  root_id := CASE WHEN kind = 0 THEN NEW."InternalConversationId" ELSE NEW."RelatedId" END;
  IF TG_OP = 'INSERT' THEN
    INSERT INTO "InternalMail"."InternalConversationGroups"
      ("ConversationId", "CompanyId", "RootConversationId", "GroupType", "DisplayName", "IsActive")
    VALUES (NEW."InternalConversationId", NEW."CompanyId", root_id, kind,
      CASE WHEN kind = 3 THEN NEW."Subject" ELSE NULL END, NEW."IsActive");
  ELSE
    SELECT * INTO mapped FROM "InternalMail"."InternalConversationGroups" WHERE "ConversationId" = NEW."InternalConversationId";
    IF NOT FOUND OR (mapped."CompanyId", mapped."RootConversationId", mapped."GroupType")
        IS DISTINCT FROM (NEW."CompanyId", root_id, kind) THEN
      RAISE EXCEPTION 'Legacy conversation disagrees with registered group structure';
    END IF;
    UPDATE "InternalMail"."InternalConversationGroups" SET "IsActive" = NEW."IsActive",
      "DisplayName" = CASE WHEN kind = 3 AND NEW."Subject" IS DISTINCT FROM OLD."Subject"
        THEN NEW."Subject" ELSE "DisplayName" END
    WHERE "ConversationId" = NEW."InternalConversationId"
      AND ("IsActive" IS DISTINCT FROM NEW."IsActive" OR
        (kind = 3 AND NEW."Subject" IS DISTINCT FROM OLD."Subject"));
  END IF;
  RETURN NEW;
END
$bridge$;
CREATE TRIGGER "TR_SyncLegacyConversationGroup"
  AFTER INSERT OR UPDATE OF "IsActive", "Subject", "RelatedType", "RelatedId", "CompanyId"
  ON "InternalMail"."InternalConversations" FOR EACH ROW
  EXECUTE FUNCTION "InternalMail"."SyncLegacyConversationGroup"();

DO $verify$
DECLARE baseline record; row_total bigint; row_hash text;
BEGIN
  IF (SELECT count(*) FROM "InternalMail"."InternalConversations") <>
     (SELECT count(*) FROM "InternalMail"."InternalConversationGroups") THEN
    RAISE EXCEPTION 'Mapping coverage mismatch';
  END IF;
  IF EXISTS (SELECT 1 FROM "InternalMail"."InternalConversations" c
      JOIN "InternalMail"."InternalConversationGroups" g ON g."ConversationId" = c."InternalConversationId"
      WHERE g."CompanyId" IS DISTINCT FROM c."CompanyId" OR g."IsActive" IS DISTINCT FROM c."IsActive"
         OR g."GroupType" <> CASE c."RelatedType" WHEN 17 THEN 1 WHEN 18 THEN 2 WHEN 19 THEN 3 ELSE 0 END
         OR g."RootConversationId" IS DISTINCT FROM CASE WHEN c."RelatedType" IN (17,18,19) THEN c."RelatedId" ELSE c."InternalConversationId" END
         OR (g."GroupType" = 3 AND g."DisplayName" IS DISTINCT FROM c."Subject")) THEN
    RAISE EXCEPTION 'Mapping values differ from legacy source';
  END IF;
  FOR baseline IN SELECT * FROM group_storage_baseline LOOP
    EXECUTE format('SELECT count(*), md5(coalesce(string_agg(md5(to_jsonb(t)::text), '''' ORDER BY md5(to_jsonb(t)::text)), '''')) FROM %s t', baseline.relation)
      INTO row_total, row_hash;
    IF row_total <> baseline.row_count OR row_hash <> baseline.fingerprint THEN
      RAISE EXCEPTION 'Protected table changed during expansion: %', baseline.relation;
    END IF;
  END LOOP;
END
$verify$;

SELECT "GroupType", count(*) AS mapped_rows FROM "InternalMail"."InternalConversationGroups" GROUP BY 1 ORDER BY 1;
COMMIT;
-- GROUP-STORAGE-TRANSITION: backfill-bridge — one-time deployment; do not remove DB bridge until all old writers are replaced.
