BEGIN;

-- Rollback discards default configuration entered after deployment; back up before running.
ALTER TABLE devandqa."EvaluationStandard"
    DROP CONSTRAINT ck_evaluationstandard_datatype,
    DROP COLUMN datatype,
    DROP COLUMN unit,
    DROP COLUMN testmethod,
    DROP COLUMN inputconfig,
    DROP COLUMN acceptancerule,
    DROP COLUMN specificationtext,
    DROP COLUMN isrequired,
    DROP COLUMN showonreport,
    DROP COLUMN showoncoa;

COMMIT;
