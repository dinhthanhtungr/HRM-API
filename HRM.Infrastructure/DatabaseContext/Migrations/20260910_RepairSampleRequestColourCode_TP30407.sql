-- One-time repair for TP_30407: GT55001 -> GT54001.
--
-- HOW TO USE IN PGADMIN
-- 1. Run the whole script. It intentionally ends with ROLLBACK.
-- 2. Review the NOTICE output and the verification SELECT statements.
-- 3. When the preview is correct, change the final ROLLBACK to COMMIT and run the whole script again.
--
-- Historical message bodies/payloads and existing AuditLogs are intentionally immutable.
-- This repair updates current/draft data, repairs navigation links, and appends a correction AuditLog.

BEGIN;

DO $repair$
DECLARE
    v_sample_request_external_id constant text := 'TP_30407';
    v_old_colour_code constant text := 'GT55001';
    v_new_colour_code text := 'GT54001';
    -- Optional: set the employee id that performs the repair. NULL preserves current UpdatedBy values.
    v_changed_by uuid := NULL;

    v_product_id uuid;
    v_company_id uuid;
    v_now timestamp without time zone := localtimestamp;
    v_product_count integer;
    v_draft_trial_count integer;
    v_sample_conversation_count integer;
    v_draft_quotation_line_count integer;
    v_draft_quotation_count integer;
    v_quotation_conversation_count integer;
    v_notification_link_count integer;
BEGIN
    v_new_colour_code := upper(btrim(v_new_colour_code));

    IF v_new_colour_code = '' OR length(v_new_colour_code) > 100 THEN
        RAISE EXCEPTION 'New ColourCode must contain 1-100 characters.';
    END IF;

    IF upper(v_new_colour_code) = upper(v_old_colour_code) THEN
        RAISE EXCEPTION 'New ColourCode is the same as the old ColourCode.';
    END IF;

    SELECT sr."ProductId", sr."CompanyId"
    INTO v_product_id, v_company_id
    FROM "SampleRequests"."SampleRequests" sr
    INNER JOIN "SampleRequests"."Products" p
        ON p."ProductId" = sr."ProductId"
       AND p."CompanyId" = sr."CompanyId"
    WHERE sr."ExternalId" = v_sample_request_external_id
      AND sr."IsActive" = true
      AND p."IsActive" = true
      AND p."ColourCode" = v_old_colour_code
    FOR UPDATE OF sr, p;

    GET DIAGNOSTICS v_product_count = ROW_COUNT;

    IF v_product_count <> 1 OR v_product_id IS NULL OR v_company_id IS NULL THEN
        RAISE EXCEPTION
            'Expected exactly one active Sample Request/Product for % with ColourCode %, found %.',
            v_sample_request_external_id,
            v_old_colour_code,
            v_product_count;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM "SampleRequests"."Products" other_product
        WHERE other_product."ProductId" <> v_product_id
          AND other_product."ColourCode" = v_new_colour_code
    ) THEN
        RAISE EXCEPTION 'New ColourCode % already exists.', v_new_colour_code;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM "SampleRequests"."SampleRequestSampleTrials" trial
        INNER JOIN "SampleRequests"."SampleRequests" sr
            ON sr."SampleRequestId" = trial."SampleRequestId"
        WHERE sr."CompanyId" = v_company_id
          AND sr."ProductId" = v_product_id
          AND trial."IsActive" = true
          AND (trial."SentDate" IS NOT NULL OR trial."Status" <> 'Draft')
    ) THEN
        RAISE EXCEPTION 'Repair stopped: this Product already has non-draft or sent sample-trial history.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM "SampleRequests"."SampleRequests" sr
        WHERE sr."CompanyId" = v_company_id
          AND sr."ProductId" = v_product_id
          AND sr."IsActive" = true
          AND sr."Status" IN ('SampleSent', 'Completed', 'FormulaUpdateRequested')
    ) THEN
        RAISE EXCEPTION 'Repair stopped: this Product is used by a sent or completed Sample Request.';
    END IF;

    UPDATE "SampleRequests"."Products"
    SET "ColourCode" = v_new_colour_code,
        "UpdatedBy" = COALESCE(v_changed_by, "UpdatedBy"),
        "UpdatedDate" = v_now
    WHERE "ProductId" = v_product_id
      AND "CompanyId" = v_company_id
      AND "IsActive" = true
      AND "ColourCode" = v_old_colour_code;

    GET DIAGNOSTICS v_product_count = ROW_COUNT;
    IF v_product_count <> 1 THEN
        RAISE EXCEPTION 'Product update affected % rows instead of 1.', v_product_count;
    END IF;

    UPDATE "SampleRequests"."SampleRequestSampleTrials" trial
    SET "ColourCodeSnapshot" = v_new_colour_code,
        "UpdatedBy" = COALESCE(v_changed_by, trial."UpdatedBy"),
        "UpdatedDate" = v_now
    FROM "SampleRequests"."SampleRequests" sr
    WHERE sr."SampleRequestId" = trial."SampleRequestId"
      AND sr."CompanyId" = v_company_id
      AND sr."ProductId" = v_product_id
      AND trial."IsActive" = true
      AND trial."Status" = 'Draft';

    GET DIAGNOSTICS v_draft_trial_count = ROW_COUNT;

    UPDATE "InternalMail"."InternalConversations" conversation
    SET "Subject" = sr."ExternalId" || ' - ' || v_new_colour_code
    FROM "SampleRequests"."SampleRequests" sr
    WHERE conversation."CompanyId" = v_company_id
      AND conversation."IsActive" = true
      AND conversation."RelatedType" = 5 -- InternalMailRelatedType.SampleRequest
      AND conversation."RelatedId" = sr."SampleRequestId"
      AND sr."CompanyId" = v_company_id
      AND sr."ProductId" = v_product_id
      AND sr."IsActive" = true;

    GET DIAGNOSTICS v_sample_conversation_count = ROW_COUNT;

    CREATE TEMP TABLE repair_draft_quotations ON COMMIT DROP AS
    SELECT DISTINCT quotation."QuotationId"
    FROM "Customer"."Quotations" quotation
    INNER JOIN "Customer"."QuotationLines" line
        ON line."QuotationId" = quotation."QuotationId"
    WHERE quotation."CompanyId" = v_company_id
      AND quotation."IsActive" = true
      AND quotation."Status" = 0 -- QuotationStatus.Draft
      AND line."IsActive" = true
      AND line."ProductId" = v_product_id;

    UPDATE "Customer"."QuotationLines" line
    SET "ProductExternalIdSnapshot" = v_new_colour_code
    FROM repair_draft_quotations repair
    WHERE line."QuotationId" = repair."QuotationId"
      AND line."ProductId" = v_product_id
      AND line."IsActive" = true;

    GET DIAGNOSTICS v_draft_quotation_line_count = ROW_COUNT;

    UPDATE "Customer"."Quotations" quotation
    SET "UpdatedBy" = COALESCE(v_changed_by, quotation."UpdatedBy"),
        "UpdatedDate" = v_now
    FROM repair_draft_quotations repair
    WHERE quotation."QuotationId" = repair."QuotationId";

    GET DIAGNOSTICS v_draft_quotation_count = ROW_COUNT;

    UPDATE "InternalMail"."InternalConversations" conversation
    SET "Subject" = subject_value."Subject"
    FROM (
        SELECT
            quotation."QuotationId",
            'Báo giá ' || btrim(quotation."ExternalId"::text) ||
            CASE
                WHEN code_list."ProductCodes" IS NULL OR code_list."ProductCodes" = '' THEN ''
                ELSE ' - ' || code_list."ProductCodes"
            END AS "Subject"
        FROM "Customer"."Quotations" quotation
        INNER JOIN repair_draft_quotations repair
            ON repair."QuotationId" = quotation."QuotationId"
        LEFT JOIN LATERAL (
            SELECT string_agg(code."ProductCode", ', ' ORDER BY code."FirstSortOrder", code."FirstLineId") AS "ProductCodes"
            FROM (
                SELECT DISTINCT ON (lower(btrim(line."ProductExternalIdSnapshot"::text)))
                    btrim(line."ProductExternalIdSnapshot"::text) AS "ProductCode",
                    line."SortOrder" AS "FirstSortOrder",
                    line."QuotationLineId"::text AS "FirstLineId"
                FROM "Customer"."QuotationLines" line
                WHERE line."QuotationId" = quotation."QuotationId"
                  AND line."IsActive" = true
                  AND btrim(line."ProductExternalIdSnapshot"::text) <> ''
                ORDER BY
                    lower(btrim(line."ProductExternalIdSnapshot"::text)),
                    line."SortOrder",
                    line."QuotationLineId"
            ) code
        ) code_list ON true
    ) subject_value
    WHERE conversation."CompanyId" = v_company_id
      AND conversation."IsActive" = true
      AND conversation."RelatedType" = 12 -- InternalMailRelatedType.Quotation
      AND conversation."RelatedId" = subject_value."QuotationId";

    GET DIAGNOSTICS v_quotation_conversation_count = ROW_COUNT;

    -- Repair navigation metadata only. Historical notification messages/payloads stay immutable.
    UPDATE notification.notifications notification
    SET link = replace(notification.link, v_old_colour_code, v_new_colour_code)
    WHERE notification.company_id = v_company_id
      AND notification.link IS NOT NULL
      AND notification.link LIKE '%product-pricing-options%'
      AND notification.link LIKE '%' || v_old_colour_code || '%';

    GET DIAGNOSTICS v_notification_link_count = ROW_COUNT;

    INSERT INTO "Audit"."AuditLogs" (
        "AuditLogId",
        "CompanyId",
        "SchemaName",
        "TableName",
        "RecordId",
        "ActionType",
        "ChangedBy",
        "ChangedAt",
        "OldValues",
        "NewValues",
        "ChangedValues",
        "Reason",
        "CorrelationId"
    )
    VALUES (
        gen_random_uuid(),
        v_company_id,
        'SampleRequests',
        'Products',
        v_product_id,
        1, -- AuditActionType.Update
        v_changed_by,
        v_now,
        jsonb_build_object('ColourCode', v_old_colour_code),
        jsonb_build_object('ColourCode', v_new_colour_code),
        jsonb_build_object(
            'ColourCode',
            jsonb_build_object('Old', v_old_colour_code, 'New', v_new_colour_code)),
        'OneTimeSystemColourCodeCorrection',
        gen_random_uuid()
    );

    RAISE NOTICE 'ProductId: %', v_product_id;
    RAISE NOTICE 'CompanyId: %', v_company_id;
    RAISE NOTICE 'ColourCode: % -> %', v_old_colour_code, v_new_colour_code;
    RAISE NOTICE 'Draft trials updated: %', v_draft_trial_count;
    RAISE NOTICE 'Sample Request conversation subjects updated: %', v_sample_conversation_count;
    RAISE NOTICE 'Draft quotation lines updated: %', v_draft_quotation_line_count;
    RAISE NOTICE 'Draft quotations touched: %', v_draft_quotation_count;
    RAISE NOTICE 'Draft quotation conversation subjects updated: %', v_quotation_conversation_count;
    RAISE NOTICE 'Notification navigation links repaired: %', v_notification_link_count;
END
$repair$;

-- Verification: these rows include the uncommitted repair while the transaction remains open.
SELECT
    sr."ExternalId" AS sample_request_code,
    sr."Status" AS sample_request_status,
    p."ProductId" AS product_id,
    p."ColourCode" AS current_colour_code,
    p."UpdatedDate" AS product_updated_date
FROM "SampleRequests"."SampleRequests" sr
INNER JOIN "SampleRequests"."Products" p ON p."ProductId" = sr."ProductId"
WHERE sr."ExternalId" = 'TP_30407';

SELECT
    conversation."InternalConversationId",
    conversation."RelatedType",
    conversation."RelatedId",
    conversation."Subject"
FROM "InternalMail"."InternalConversations" conversation
WHERE conversation."IsActive" = true
  AND conversation."Subject" ILIKE '%TP_30407%';

SELECT
    audit."ChangedAt",
    audit."Reason",
    audit."ChangedValues"
FROM "Audit"."AuditLogs" audit
WHERE audit."Reason" = 'OneTimeSystemColourCodeCorrection'
ORDER BY audit."ChangedAt" DESC
LIMIT 5;

-- Safety default. Replace with COMMIT only after reviewing the NOTICE output and verification queries.
ROLLBACK;

