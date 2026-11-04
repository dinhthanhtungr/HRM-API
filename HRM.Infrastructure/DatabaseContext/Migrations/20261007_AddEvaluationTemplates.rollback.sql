-- Run only before new template/method data is used. Never discard new inspection data.
BEGIN;
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM devandqa."EvaluationTemplate")
 OR EXISTS (SELECT 1 FROM devandqa."MeasurementMethod")
 OR EXISTS (SELECT 1 FROM devandqa."IqcInspectionEvaluation" WHERE iqcspecificationid IS NULL)
 OR EXISTS (SELECT 1 FROM devandqa."CoaCertificate" WHERE coaspecificationid IS NULL)
 OR EXISTS (SELECT 1 FROM devandqa."IqcInspectionResult" WHERE iqcspecificationitemid IS NULL)
 OR EXISTS (SELECT 1 FROM devandqa."CoaCertificateResult" WHERE coaspecificationitemid IS NULL)
 THEN RAISE EXCEPTION 'Rollback blocked: new data exists; preserve it or restore a reviewed backup.';
 END IF;
END $$;
ALTER TABLE devandqa."IqcInspectionResult" DROP CONSTRAINT ck_iqcinspectionresult_snapshot,
 DROP COLUMN sourcetemplateitemid,
 DROP COLUMN evaluationstandardid,
 DROP COLUMN hasconfigurationsnapshot,
 DROP COLUMN criterioncode,
 DROP COLUMN criterionname,
 DROP COLUMN datatype,
 DROP COLUMN unit,
 DROP COLUMN measurementmethodid,
 DROP COLUMN measurementmethodcode,
 DROP COLUMN testmethod,
 DROP COLUMN inputconfig,
 DROP COLUMN acceptancerule,
 DROP COLUMN specificationtext,
 DROP COLUMN isrequired,
 DROP COLUMN showonreport,
 DROP COLUMN showoncoa,
 DROP COLUMN sortorder,
 ALTER COLUMN iqcspecificationitemid SET NOT NULL;
ALTER TABLE devandqa."CoaCertificateResult" DROP CONSTRAINT ck_coacertificateresult_snapshot,
 DROP COLUMN sourcetemplateitemid,
 DROP COLUMN evaluationstandardid,
 DROP COLUMN hasconfigurationsnapshot,
 DROP COLUMN criterioncode,
 DROP COLUMN criterionname,
 DROP COLUMN datatype,
 DROP COLUMN unit,
 DROP COLUMN measurementmethodid,
 DROP COLUMN measurementmethodcode,
 DROP COLUMN testmethod,
 DROP COLUMN inputconfig,
 DROP COLUMN acceptancerule,
 DROP COLUMN specificationtext,
 DROP COLUMN isrequired,
 DROP COLUMN showonreport,
 DROP COLUMN showoncoa,
 DROP COLUMN sortorder,
 ALTER COLUMN coaspecificationitemid SET NOT NULL;
ALTER TABLE devandqa."IqcInspectionEvaluation" DROP COLUMN evaluationtemplateid, DROP COLUMN templatecode, DROP COLUMN templatename, DROP COLUMN templateversion, ALTER COLUMN iqcspecificationid SET NOT NULL;
ALTER TABLE devandqa."CoaCertificate" DROP COLUMN evaluationtemplateid, DROP COLUMN templatecode, DROP COLUMN templatename, DROP COLUMN templateversion, ALTER COLUMN coaspecificationid SET NOT NULL;
ALTER TABLE devandqa."EvaluationStandard" DROP COLUMN measurementmethodid;
ALTER TABLE devandqa."IqcSpecificationItem" DROP COLUMN measurementmethodid, DROP COLUMN measurementmethodcode;
ALTER TABLE devandqa."CoaSpecificationItem" DROP COLUMN measurementmethodid, DROP COLUMN measurementmethodcode;
DROP TABLE devandqa."EvaluationTemplateItem";
DROP TABLE devandqa."EvaluationTemplate";
DROP TABLE devandqa."MeasurementMethod";
COMMIT;
