-- READ ONLY. Run against the intended database before any DDL.
-- Does not prove migration execution; compare with actual migration history separately.
SELECT table_name, column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'devandqa'
  AND table_name IN ('EvaluationStandard','MeasurementMethod','EvaluationTemplate','EvaluationTemplateItem',
                    'IqcSpecification','IqcSpecificationItem','IqcInspectionEvaluation','IqcInspectionResult',
                    'CoaSpecification','CoaSpecificationItem','CoaCertificate','CoaCertificateResult')
ORDER BY table_name, ordinal_position;

-- Inspect additional FK columns recorded in the old migration.
SELECT table_name, column_name, is_nullable
FROM information_schema.columns
WHERE table_schema = 'devandqa'
  AND (column_name IN ('CompanyId1','CreatedByEmployeeEmployeeId','ApprovedByEmployeeEmployeeId')
       OR column_name LIKE '%EmployeeEmployeeId')
ORDER BY table_name, column_name;

-- Actual FK names/columns/delete actions are needed before a corrective migration.
SELECT c.relname AS table_name, con.conname AS constraint_name,
       pg_get_constraintdef(con.oid) AS definition
FROM pg_constraint con
JOIN pg_class c ON c.oid = con.conrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'devandqa' AND con.contype = 'f'
  AND c.relname IN ('EvaluationStandard','IqcSpecification','CoaSpecification','CoaCertificate')
ORDER BY c.relname, con.conname;
