BEGIN;

-- Requires the existing catalog table. Old rows retain a Text default without inferred rules.
ALTER TABLE devandqa."EvaluationStandard"
    ADD COLUMN datatype integer NOT NULL DEFAULT 1,
    ADD COLUMN unit varchar(50),
    ADD COLUMN testmethod text,
    ADD COLUMN inputconfig jsonb,
    ADD COLUMN acceptancerule jsonb,
    ADD COLUMN specificationtext text,
    ADD COLUMN isrequired boolean NOT NULL DEFAULT false,
    ADD COLUMN showonreport boolean NOT NULL DEFAULT true,
    ADD COLUMN showoncoa boolean NOT NULL DEFAULT true;

ALTER TABLE devandqa."EvaluationStandard"
    ADD CONSTRAINT ck_evaluationstandard_datatype CHECK (datatype BETWEEN 1 AND 7);

COMMIT;
