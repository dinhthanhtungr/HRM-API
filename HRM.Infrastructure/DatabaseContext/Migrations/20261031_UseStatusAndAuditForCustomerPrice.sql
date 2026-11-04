-- No new tables/columns. Run before deploying the status/audit API if the six-column prototype was used.
-- Safe on databases without that prototype: only capability claims are seeded.
BEGIN;
DO $$
BEGIN
    IF (SELECT count(*) = 6 FROM information_schema.columns
        WHERE table_schema = 'SampleRequests' AND table_name = 'SampleRequests' AND column_name IN
        ('SampleWorkflowVersion','CustomerPriceConfirmedAt','CustomerPriceConfirmedByEmployeeId',
         'CustomerPriceQuotationLineId','CustomerPriceReference','CustomerPriceConfirmationNote')) THEN
        EXECUTE $import$
            INSERT INTO "Audit"."AuditLogs" ("AuditLogId","CompanyId","SchemaName","TableName","RecordId",
                "ActionType","ChangedBy","ChangedAt","Reason","NewValues")
            SELECT gen_random_uuid(),s."CompanyId",'SampleRequests','SampleRequests',s."SampleRequestId",1,
                s."CustomerPriceConfirmedByEmployeeId",s."CustomerPriceConfirmedAt",'CustomerPriceAccepted',
                jsonb_build_object('Status',s."Status",'CustomerAcceptedAt',NULL,
                    'QuotationLineId',s."CustomerPriceQuotationLineId",'PriceReference',s."CustomerPriceReference",
                    'Note',s."CustomerPriceConfirmationNote",'ImportedFromPrototype',true)
            FROM "SampleRequests"."SampleRequests" s
            WHERE s."CustomerPriceConfirmedAt" IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM "Audit"."AuditLogs" a WHERE a."CompanyId"=s."CompanyId"
                AND a."SchemaName"='SampleRequests' AND a."TableName"='SampleRequests'
                AND a."RecordId"=s."SampleRequestId" AND a."Reason" IN
                ('CustomerPriceAccepted','CustomerPriceAcceptanceRevoked','CustomerPriceConfirmed','CustomerPriceConfirmationRevoked'))
        $import$;
        EXECUTE $mark$
            INSERT INTO "Audit"."AuditLogs" ("AuditLogId","CompanyId","SchemaName","TableName","RecordId",
                "ActionType","ChangedBy","ChangedAt","Reason","NewValues")
            SELECT gen_random_uuid(),s."CompanyId",'SampleRequests','SampleRequests',s."SampleRequestId",0,
                s."CreatedBy",s."CreatedDate",'SampleRequestWorkflowCreated',jsonb_build_object('Status',s."Status",'ImportedFromPrototype',true)
            FROM "SampleRequests"."SampleRequests" s WHERE s."SampleWorkflowVersion">=1 AND NOT EXISTS (
                SELECT 1 FROM "Audit"."AuditLogs" a WHERE a."CompanyId"=s."CompanyId"
                AND a."SchemaName"='SampleRequests' AND a."TableName"='SampleRequests'
                AND a."RecordId"=s."SampleRequestId" AND a."Reason"='SampleRequestWorkflowCreated')
        $mark$;
        EXECUTE $pending$
            WITH changed AS (
                UPDATE "SampleRequests"."SampleRequests" s SET "Status"='WaitingCustomerPriceConfirmation', "UpdatedDate"=LOCALTIMESTAMP
                WHERE s."SampleWorkflowVersion">=1 AND s."CustomerPriceConfirmedAt" IS NULL
                  AND s."Status" IN ('New','Pending','InProgress')
                  AND (lower(trim(s."InfoType")) LIKE 'thẻ màu%' OR lower(trim(s."InfoType")) IN ('colorsampleset','coloursampleset'))
                  AND NOT EXISTS (SELECT 1 FROM "SampleRequests"."SampleRequestSampleTrials" t
                    WHERE t."SampleRequestId"=s."SampleRequestId" AND t."IsActive" AND t."SentDate" IS NOT NULL)
                  AND NOT EXISTS (SELECT 1 FROM "Audit"."AuditLogs" a WHERE a."CompanyId"=s."CompanyId"
                    AND a."SchemaName"='SampleRequests' AND a."TableName"='SampleRequests'
                    AND a."RecordId"=s."SampleRequestId" AND a."Reason" IN ('CustomerPriceAccepted','CustomerPriceAcceptanceRevoked'))
                RETURNING s."SampleRequestId",s."CompanyId"
            ) INSERT INTO "Audit"."AuditLogs" ("AuditLogId","CompanyId","SchemaName","TableName","RecordId","ActionType","ChangedAt","Reason","NewValues")
              SELECT gen_random_uuid(),"CompanyId",'SampleRequests','SampleRequests',"SampleRequestId",1,LOCALTIMESTAMP,
                'CustomerPriceWorkflowMigrated',jsonb_build_object('Status','WaitingCustomerPriceConfirmation') FROM changed
        $pending$;
    ELSIF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='SampleRequests'
        AND table_name='SampleRequests' AND column_name IN
        ('SampleWorkflowVersion','CustomerPriceConfirmedAt','CustomerPriceConfirmedByEmployeeId',
         'CustomerPriceQuotationLineId','CustomerPriceReference','CustomerPriceConfirmationNote')) THEN
        RAISE EXCEPTION 'Partial prototype schema detected. Review existing columns before importing price confirmation data.';
    END IF;
END $$;

WITH permissions(role_name, permission) AS (
    VALUES
    ('SaleUser','plm.sample-request.customer-price.confirm'),('Leader','plm.sample-request.customer-price.confirm'),
    ('Admin','plm.sample-request.customer-price.confirm'),('Developer','plm.sample-request.customer-price.confirm'),
    ('President','plm.sample-request.customer-price.confirm'),('LabAdmin','plm.sample-request.lab-standard.receive'),
    ('Admin','plm.sample-request.lab-standard.receive'),('Developer','plm.sample-request.lab-standard.receive'),
    ('President','plm.sample-request.lab-standard.receive')
)
INSERT INTO "AspNetRoleClaims" ("RoleId","ClaimType","ClaimValue")
SELECT r."Id",'permission',p.permission FROM permissions p JOIN "AspNetRoles" r ON lower(r."Name")=lower(p.role_name)
WHERE NOT EXISTS (SELECT 1 FROM "AspNetRoleClaims" c WHERE c."RoleId"=r."Id"
    AND c."ClaimType"='permission' AND c."ClaimValue"=p.permission);
COMMIT;
-- Prototype columns are deliberately retained. Application code no longer maps or queries them.
