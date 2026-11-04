-- OPTIONAL manual step when deployer and API runtime use different DB roles.
-- No role/capability business mapping is changed. No PUBLIC grant, no SECURITY DEFINER.
-- Run only after approved expansion, with an explicit existing runtime role in this session.
BEGIN;
SET LOCAL lock_timeout = '3s';
SET LOCAL statement_timeout = '15s';
DO $runtime_grants$
DECLARE runtime_role text := nullif(btrim(current_setting('hrm.group_storage_runtime_role', true)), '');
BEGIN
  IF current_setting('hrm.group_storage_review_approved', true) IS DISTINCT FROM 'yes' THEN
    RAISE EXCEPTION 'Explicit deployment approval is required';
  END IF;
  IF runtime_role IS NULL OR lower(runtime_role) = 'public' OR
      NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = runtime_role) THEN
    RAISE EXCEPTION 'Specify an existing, non-PUBLIC runtime DB role';
  END IF;
  IF to_regclass('"InternalMail"."InternalConversationGroups"') IS NULL THEN
    RAISE EXCEPTION 'Expand group storage first';
  END IF;
  -- Role is one quoted identifier, never interpolated as SQL syntax or read from an API payload.
  EXECUTE format('GRANT SELECT, INSERT, UPDATE ON "InternalMail"."InternalConversationGroups" TO %I', runtime_role);
END
$runtime_grants$;
COMMIT;
-- GROUP-STORAGE-TRANSITION: backfill-bridge — optional deployment-role setup; removing this file must not revoke runtime access.
