-- Synthetic fixture ONLY; caller verifies a newly initialized cluster data_directory before executing.
-- Reset auxiliary stubs as well: President/policy tests must not inherit another test's table shape or role bindings.
DROP SCHEMA IF EXISTS company CASCADE;
DROP SCHEMA IF EXISTS "Material" CASCADE;
DROP SCHEMA IF EXISTS "Customer" CASCADE;
DROP SCHEMA IF EXISTS "SampleRequests" CASCADE;
DROP TABLE IF EXISTS public."AspNetUserRoles", public."AspNetUsers", public."AspNetRoles" CASCADE;
DROP SCHEMA IF EXISTS "InternalMail" CASCADE;
DROP SCHEMA IF EXISTS notification CASCADE;
DROP SCHEMA IF EXISTS hr CASCADE;
CREATE SCHEMA "InternalMail";
CREATE SCHEMA notification;
CREATE SCHEMA hr;
CREATE TABLE hr."Employees" ("EmployeeID" uuid PRIMARY KEY, "CompanyId" uuid NOT NULL);
CREATE TABLE "InternalMail"."InternalConversations" (
 "InternalConversationId" uuid PRIMARY KEY, "CompanyId" uuid NOT NULL, "Subject" citext NOT NULL,
 "RelatedType" integer NULL, "RelatedId" uuid NULL, "RelatedExternalId" citext NULL,
 "CreatedBy" uuid NOT NULL, "CreatedAt" timestamp NOT NULL DEFAULT '2026-10-01',
 "LastMessageAt" timestamp NOT NULL DEFAULT '2026-10-01', "LastMessageId" uuid NULL,
 "IsActive" boolean NOT NULL DEFAULT true, "DeletedAt" timestamp NULL, "DeletedByEmployeeId" uuid NULL
);
CREATE TABLE "InternalMail"."InternalConversationParticipants" (
 "InternalConversationId" uuid NOT NULL REFERENCES "InternalMail"."InternalConversations" ON DELETE CASCADE,
 "EmployeeId" uuid NOT NULL REFERENCES hr."Employees", "Role" integer NOT NULL DEFAULT 1,
 "IsActive" boolean NOT NULL DEFAULT true, "IsArchived" boolean NOT NULL DEFAULT false,
 "IsMuted" boolean NOT NULL DEFAULT false, "ArchivedAt" timestamp NULL, "LastReadAt" timestamp NULL,
 "JoinedAt" timestamp NOT NULL DEFAULT '2026-10-01', "DeletedAt" timestamp NULL, "DeletedByEmployeeId" uuid NULL,
 PRIMARY KEY ("InternalConversationId", "EmployeeId")
);
CREATE TABLE "InternalMail"."InternalMessages" (
 "InternalMessageId" uuid PRIMARY KEY, "InternalConversationId" uuid NOT NULL REFERENCES "InternalMail"."InternalConversations",
 "SenderEmployeeId" uuid NOT NULL, "Body" text NOT NULL, "PayloadJson" jsonb NULL,
 "MessageType" integer NOT NULL DEFAULT 0, "IsUrgent" boolean NOT NULL DEFAULT false,
 "SentAt" timestamp NOT NULL DEFAULT '2026-10-02', "ReplyToMessageId" uuid NULL REFERENCES "InternalMail"."InternalMessages",
 "IsEdited" boolean NOT NULL DEFAULT false, "EditedAt" timestamp NULL, "EditedByEmployeeId" uuid NULL,
 "IsDeleted" boolean NOT NULL DEFAULT false, "DeletedAt" timestamp NULL, "DeletedByEmployeeId" uuid NULL
);
CREATE TABLE "InternalMail"."InternalMessageReadStates" (
 "InternalMessageId" uuid NOT NULL REFERENCES "InternalMail"."InternalMessages", "EmployeeId" uuid NOT NULL,
 "IsRead" boolean NOT NULL DEFAULT false, "ReadAt" timestamp NULL, PRIMARY KEY ("InternalMessageId", "EmployeeId")
);
CREATE TABLE "InternalMail"."InternalMessageReferences" (
 "InternalMessageReferenceId" uuid PRIMARY KEY, "InternalMessageId" uuid NOT NULL REFERENCES "InternalMail"."InternalMessages",
 "RelatedType" integer NOT NULL, "RelatedId" uuid NOT NULL, "SnapshotJson" jsonb NULL, "IsPrimary" boolean NOT NULL DEFAULT false
);
CREATE TABLE "InternalMail"."InternalMessageAttachments" (
 "InternalMessageId" uuid NOT NULL REFERENCES "InternalMail"."InternalMessages", "AttachmentId" uuid NOT NULL,
 PRIMARY KEY ("InternalMessageId", "AttachmentId")
);
CREATE TABLE notification.notifications (id uuid PRIMARY KEY, company_id uuid NOT NULL, payload_json jsonb NULL, created_date timestamp NOT NULL DEFAULT '2026-10-02', topic integer NOT NULL DEFAULT 0);
CREATE TABLE notification.notification_user_states (
 notification_id uuid NOT NULL REFERENCES notification.notifications, user_id uuid NOT NULL,
 is_read boolean NOT NULL DEFAULT false, is_archived boolean NOT NULL DEFAULT false, read_date timestamp NULL,
 PRIMARY KEY (notification_id,user_id)
);
CREATE TABLE notification.notification_recipients (id uuid PRIMARY KEY, notification_id uuid NOT NULL REFERENCES notification.notifications, target_user_id uuid NULL);
CREATE TABLE notification.outbox_messages (id uuid PRIMARY KEY, type text NOT NULL, payload_json jsonb NOT NULL,
 created_at timestamp NOT NULL DEFAULT '2026-10-02', processed_at timestamp NULL, attempts integer NOT NULL DEFAULT 0, error text NULL);
INSERT INTO hr."Employees" VALUES
 ('00000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001'),
 ('00000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000001'),
 ('00000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000002');
INSERT INTO "InternalMail"."InternalConversations" ("InternalConversationId","CompanyId","Subject","RelatedType","RelatedId","CreatedBy","IsActive") VALUES
 ('20000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','TP_fixture',5,'90000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001',true),
 ('20000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000001','Quotation fixture',12,'90000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000001',true),
 ('20000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000002','Other company',5,'90000000-0000-0000-0000-000000000003','00000000-0000-0000-0000-000000000003',true),
 ('30000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','TP_fixture',17,'20000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001',true),
 ('30000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000001','Old technical',17,'20000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001',false),
 ('30000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000001','TP_fixture',18,'20000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001',true),
 ('30000000-0000-0000-0000-000000000004','10000000-0000-0000-0000-000000000001','Thử mẫu đợt 2',19,'20000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001',true),
 ('30000000-0000-0000-0000-000000000005','10000000-0000-0000-0000-000000000001','Chốt giao hàng',19,'20000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000002',true);
INSERT INTO "InternalMail"."InternalConversationParticipants" ("InternalConversationId","EmployeeId","Role","IsMuted","IsArchived","LastReadAt")
 SELECT "InternalConversationId",'00000000-0000-0000-0000-000000000001',0,true,true,'2026-10-03'
 FROM "InternalMail"."InternalConversations" WHERE "CompanyId"='10000000-0000-0000-0000-000000000001';
INSERT INTO "InternalMail"."InternalConversationParticipants" ("InternalConversationId","EmployeeId","IsActive") VALUES
 ('30000000-0000-0000-0000-000000000004','00000000-0000-0000-0000-000000000002',false);
INSERT INTO "InternalMail"."InternalMessages" ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body","PayloadJson","IsDeleted") VALUES
 ('40000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000002','@Fixture nội dung nguồn', '{"mentionedEmployeeIds":["00000000-0000-0000-0000-000000000001"]}',false),
 ('40000000-0000-0000-0000-000000000002','30000000-0000-0000-0000-000000000004','00000000-0000-0000-0000-000000000002','Bản chuyển tiếp', '{"forwarded":true}',false),
 ('40000000-0000-0000-0000-000000000003','30000000-0000-0000-0000-000000000004','00000000-0000-0000-0000-000000000002','Tin đã xóa',null,true);
UPDATE "InternalMail"."InternalMessages" SET "ReplyToMessageId"='40000000-0000-0000-0000-000000000002' WHERE "InternalMessageId"='40000000-0000-0000-0000-000000000003';
INSERT INTO "InternalMail"."InternalMessageReadStates" ("InternalMessageId","EmployeeId","IsRead")
 SELECT "InternalMessageId",'00000000-0000-0000-0000-000000000001',false FROM "InternalMail"."InternalMessages";
INSERT INTO "InternalMail"."InternalMessageReferences" VALUES
 ('50000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000002',20,'20000000-0000-0000-0000-000000000001','{"preserve":"source"}',false);
INSERT INTO "InternalMail"."InternalMessageAttachments" VALUES
 ('40000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000001');
INSERT INTO notification.notifications (id,company_id,payload_json) VALUES
 ('70000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','{"conversationId":"30000000-0000-0000-0000-000000000004"}');
INSERT INTO notification.notification_user_states (notification_id,user_id) VALUES
 ('70000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001');
INSERT INTO notification.notification_recipients VALUES
 ('80000000-0000-0000-0000-000000000001','70000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001');
INSERT INTO notification.outbox_messages (id,type,payload_json,attempts) VALUES
 ('80000000-0000-0000-0000-000000000002','SyntheticFixture','{"preserve":"outbox"}',2);
