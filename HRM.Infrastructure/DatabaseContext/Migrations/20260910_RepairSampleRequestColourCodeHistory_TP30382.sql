-- Follow-up one-time presentation repair for TP_30382: BP36212 -> BP35003.
-- Use only after the canonical Product ColourCode has already been corrected to BP35003.
--
-- This script intentionally edits historical message/notification presentation because the old
-- ColourCode was confirmed to be a system error. Scope is limited to TP_30382 and its conversation.
-- It ends with ROLLBACK for preview. Replace the final ROLLBACK with COMMIT only after verification.

BEGIN;

DO $repair$
DECLARE
    v_sample_request_external_id constant text := 'TP_30382';
    v_old_colour_code constant text := 'BP36212';
    v_new_colour_code constant text := 'BP35003';
    v_sample_request_id uuid;
    v_product_id uuid;
    v_company_id uuid;
    v_now timestamp without time zone := localtimestamp;
    v_message_body_count integer;
    v_message_payload_count integer;
    v_notification_count integer;
BEGIN
    SELECT sr."SampleRequestId", sr."ProductId", sr."CompanyId"
    INTO v_sample_request_id, v_product_id, v_company_id
    FROM "SampleRequests"."SampleRequests" sr
    INNER JOIN "SampleRequests"."Products" product
        ON product."ProductId" = sr."ProductId"
       AND product."CompanyId" = sr."CompanyId"
    WHERE sr."ExternalId" = v_sample_request_external_id
      AND sr."IsActive" = true
      AND product."IsActive" = true
      AND product."ColourCode" = v_new_colour_code;

    IF v_sample_request_id IS NULL OR v_product_id IS NULL OR v_company_id IS NULL THEN
        RAISE EXCEPTION
            'Expected active Sample Request % with current ColourCode %, but it was not found.',
            v_sample_request_external_id,
            v_new_colour_code;
    END IF;

    UPDATE "InternalMail"."InternalMessages" message
    SET "Body" = replace(message."Body", v_old_colour_code, v_new_colour_code),
        "IsEdited" = true,
        "EditedAt" = v_now
    FROM "InternalMail"."InternalConversations" conversation
    WHERE conversation."InternalConversationId" = message."InternalConversationId"
      AND conversation."CompanyId" = v_company_id
      AND conversation."IsActive" = true
      AND conversation."RelatedType" = 5 -- InternalMailRelatedType.SampleRequest
      AND conversation."RelatedId" = v_sample_request_id
      AND message."IsDeleted" = false
      AND message."Body" LIKE '%' || v_old_colour_code || '%';

    GET DIAGNOSTICS v_message_body_count = ROW_COUNT;

    UPDATE "InternalMail"."InternalMessages" message
    SET "PayloadJson" = replace(message."PayloadJson"::text, v_old_colour_code, v_new_colour_code)::jsonb,
        "IsEdited" = true,
        "EditedAt" = COALESCE(message."EditedAt", v_now)
    FROM "InternalMail"."InternalConversations" conversation
    WHERE conversation."InternalConversationId" = message."InternalConversationId"
      AND conversation."CompanyId" = v_company_id
      AND conversation."IsActive" = true
      AND conversation."RelatedType" = 5 -- InternalMailRelatedType.SampleRequest
      AND conversation."RelatedId" = v_sample_request_id
      AND message."IsDeleted" = false
      AND message."PayloadJson" IS NOT NULL
      AND message."PayloadJson"::text LIKE '%' || v_old_colour_code || '%';

    GET DIAGNOSTICS v_message_payload_count = ROW_COUNT;

    UPDATE notification.notifications notification
    SET title = replace(notification.title, v_old_colour_code, v_new_colour_code),
        message = replace(notification.message, v_old_colour_code, v_new_colour_code),
        link = CASE
            WHEN notification.link IS NULL THEN NULL
            ELSE replace(notification.link, v_old_colour_code, v_new_colour_code)
        END,
        payload_json = CASE
            WHEN notification.payload_json IS NULL THEN NULL
            ELSE replace(notification.payload_json::text, v_old_colour_code, v_new_colour_code)::jsonb
        END
    WHERE notification.company_id = v_company_id
      AND (
          notification.message LIKE '%' || v_sample_request_external_id || '%' OR
          notification.payload_json::text LIKE '%' || v_sample_request_id::text || '%'
      )
      AND (
          notification.title LIKE '%' || v_old_colour_code || '%' OR
          notification.message LIKE '%' || v_old_colour_code || '%' OR
          notification.link LIKE '%' || v_old_colour_code || '%' OR
          notification.payload_json::text LIKE '%' || v_old_colour_code || '%'
      );

    GET DIAGNOSTICS v_notification_count = ROW_COUNT;

    INSERT INTO "Audit"."AuditLogs" (
        "AuditLogId",
        "CompanyId",
        "SchemaName",
        "TableName",
        "RecordId",
        "ActionType",
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
        'InternalMail',
        'InternalMessages',
        v_sample_request_id,
        1, -- AuditActionType.Update
        v_now,
        jsonb_build_object(
            'IncorrectColourCode', v_old_colour_code,
            'MessageBodyMatches', v_message_body_count,
            'MessagePayloadMatches', v_message_payload_count),
        jsonb_build_object(
            'CorrectedColourCode', v_new_colour_code,
            'NotificationMatches', v_notification_count),
        jsonb_build_object(
            'ColourCode', jsonb_build_object('Old', v_old_colour_code, 'New', v_new_colour_code)),
        'OneTimeSystemColourCodeHistoricalPresentationCorrection',
        gen_random_uuid()
    );

    RAISE NOTICE 'SampleRequestId: %', v_sample_request_id;
    RAISE NOTICE 'Message bodies corrected: %', v_message_body_count;
    RAISE NOTICE 'Message payloads corrected: %', v_message_payload_count;
    RAISE NOTICE 'Notifications corrected: %', v_notification_count;
END
$repair$;

-- Verification: no old code should remain in the visible thread content for TP_30382.
SELECT
    message."InternalMessageId",
    message."Body",
    message."IsEdited",
    message."EditedAt",
    message."PayloadJson"
FROM "InternalMail"."InternalMessages" message
INNER JOIN "InternalMail"."InternalConversations" conversation
    ON conversation."InternalConversationId" = message."InternalConversationId"
INNER JOIN "SampleRequests"."SampleRequests" sr
    ON sr."SampleRequestId" = conversation."RelatedId"
WHERE conversation."RelatedType" = 5
  AND sr."ExternalId" = 'TP_30382'
ORDER BY message."SentAt";

SELECT
    notification.id,
    notification.title,
    notification.message,
    notification.link,
    notification.payload_json
FROM notification.notifications notification
WHERE notification.message LIKE '%TP_30382%'
   OR notification.payload_json::text LIKE '%TP_30382%'
ORDER BY notification.created_date;

-- Old AuditLogs remain unchanged. This query should return zero for visible message/notification data.
SELECT source, remaining_matches
FROM (
    SELECT
        'InternalMessages' AS source,
        count(*) AS remaining_matches
    FROM "InternalMail"."InternalMessages" message
    INNER JOIN "InternalMail"."InternalConversations" conversation
        ON conversation."InternalConversationId" = message."InternalConversationId"
    INNER JOIN "SampleRequests"."SampleRequests" sr
        ON sr."SampleRequestId" = conversation."RelatedId"
    WHERE conversation."RelatedType" = 5
      AND sr."ExternalId" = 'TP_30382'
      AND (message."Body" LIKE '%BP36212%' OR message."PayloadJson"::text LIKE '%BP36212%')

    UNION ALL

    SELECT
        'Notifications' AS source,
        count(*) AS remaining_matches
    FROM notification.notifications notification
    WHERE (notification.message LIKE '%TP_30382%' OR notification.payload_json::text LIKE '%TP_30382%')
      AND (
          notification.title LIKE '%BP36212%' OR
          notification.message LIKE '%BP36212%' OR
          notification.link LIKE '%BP36212%' OR
          notification.payload_json::text LIKE '%BP36212%'
      )
) verification;

-- Safety default. Replace with COMMIT only after both remaining_matches values are zero.
ROLLBACK;


