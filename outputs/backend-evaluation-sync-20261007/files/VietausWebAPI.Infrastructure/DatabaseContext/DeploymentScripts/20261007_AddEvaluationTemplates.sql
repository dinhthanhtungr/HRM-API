-- Reviewed deployment script only: NOT executed by the coding agent.
-- Prerequisite: existing nine dynamic IQC/COA tables and EvaluationStandard default columns.
-- Back up first. Execute once, before running the application with the new model.
BEGIN;
CREATE TABLE devandqa."MeasurementMethod" (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 companyid uuid NOT NULL REFERENCES company."Companies"("companyId") ON DELETE RESTRICT,
 code citext NOT NULL, name varchar(200) NOT NULL, description text,
 isactive boolean NOT NULL DEFAULT true, sortorder integer NOT NULL DEFAULT 0,
 createdby uuid NOT NULL REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
 createddate timestamp without time zone NOT NULL,
 updatedby uuid REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
 updateddate timestamp without time zone,
 CONSTRAINT ck_measurementmethod_sortorder CHECK (sortorder >= 0),
 UNIQUE(companyid, code)
);
CREATE TABLE devandqa."EvaluationTemplate" (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 companyid uuid NOT NULL REFERENCES company."Companies"("companyId") ON DELETE RESTRICT,
 code citext NOT NULL, name varchar(200) NOT NULL,
 type integer NOT NULL, version integer NOT NULL, status integer NOT NULL, isdefault boolean NOT NULL,
 effectivefrom timestamp without time zone,
 createdby uuid NOT NULL REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
 createddate timestamp without time zone NOT NULL,
 publishedby uuid REFERENCES hr."Employees"("EmployeeID") ON DELETE RESTRICT,
 publisheddate timestamp without time zone,
 CONSTRAINT ck_evaluationtemplate_version CHECK (version > 0),
 CONSTRAINT ck_evaluationtemplate_type CHECK (type IN (1,2,3)),
 CONSTRAINT ck_evaluationtemplate_status CHECK (status IN (0,1,2)),
 CONSTRAINT ck_evaluationtemplate_default CHECK (NOT isdefault OR status = 1),
 UNIQUE(companyid,type,code,version)
);
CREATE UNIQUE INDEX ux_evaluationtemplate_default ON devandqa."EvaluationTemplate"(companyid,type)
 WHERE isdefault = TRUE AND status = 1;
CREATE TABLE devandqa."EvaluationTemplateItem" (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 evaluationtemplateid uuid NOT NULL REFERENCES devandqa."EvaluationTemplate"(id) ON DELETE RESTRICT,
 evaluationstandardid uuid NOT NULL REFERENCES devandqa."EvaluationStandard"(id) ON DELETE RESTRICT,
 criterioncode citext NOT NULL DEFAULT '',
 criterionname varchar(200) NOT NULL DEFAULT '',
 datatype integer NOT NULL DEFAULT 1,
 unit varchar(50),
 measurementmethodid uuid,
 measurementmethodcode varchar(50),
 testmethod text,
 inputconfig jsonb,
 acceptancerule jsonb,
 specificationtext text,
 isrequired boolean NOT NULL DEFAULT false,
 showonreport boolean NOT NULL DEFAULT true,
 showoncoa boolean NOT NULL DEFAULT true,
 sortorder integer NOT NULL DEFAULT 0,
 FOREIGN KEY(measurementmethodid) REFERENCES devandqa."MeasurementMethod"(id) ON DELETE RESTRICT,
 UNIQUE(evaluationtemplateid,evaluationstandardid),
 UNIQUE(evaluationtemplateid,criterioncode)
);
ALTER TABLE devandqa."EvaluationStandard" ADD COLUMN measurementmethodid uuid REFERENCES devandqa."MeasurementMethod"(id) ON DELETE RESTRICT;
ALTER TABLE devandqa."IqcSpecificationItem" ADD COLUMN measurementmethodid uuid REFERENCES devandqa."MeasurementMethod"(id) ON DELETE RESTRICT;
ALTER TABLE devandqa."IqcSpecificationItem" ADD COLUMN measurementmethodcode varchar(50);
ALTER TABLE devandqa."CoaSpecificationItem" ADD COLUMN measurementmethodid uuid REFERENCES devandqa."MeasurementMethod"(id) ON DELETE RESTRICT;
ALTER TABLE devandqa."CoaSpecificationItem" ADD COLUMN measurementmethodcode varchar(50);
ALTER TABLE devandqa."IqcInspectionEvaluation"
 ALTER COLUMN iqcspecificationid DROP NOT NULL,
 ADD COLUMN evaluationtemplateid uuid REFERENCES devandqa."EvaluationTemplate"(id) ON DELETE RESTRICT,
 ADD COLUMN templatecode varchar(50), ADD COLUMN templatename varchar(200), ADD COLUMN templateversion integer;
ALTER TABLE devandqa."CoaCertificate"
 ALTER COLUMN coaspecificationid DROP NOT NULL,
 ADD COLUMN evaluationtemplateid uuid REFERENCES devandqa."EvaluationTemplate"(id) ON DELETE RESTRICT,
 ADD COLUMN templatecode varchar(50), ADD COLUMN templatename varchar(200), ADD COLUMN templateversion integer;
ALTER TABLE devandqa."IqcInspectionResult"
 ALTER COLUMN iqcspecificationitemid DROP NOT NULL,
 ADD COLUMN sourcetemplateitemid uuid REFERENCES devandqa."EvaluationTemplateItem"(id) ON DELETE RESTRICT,
 ADD COLUMN evaluationstandardid uuid REFERENCES devandqa."EvaluationStandard"(id) ON DELETE RESTRICT,
 ADD COLUMN hasconfigurationsnapshot boolean NOT NULL DEFAULT false,
 ADD COLUMN criterioncode citext NOT NULL DEFAULT '',
 ADD COLUMN criterionname varchar(200) NOT NULL DEFAULT '',
 ADD COLUMN datatype integer NOT NULL DEFAULT 1,
 ADD COLUMN unit varchar(50),
 ADD COLUMN measurementmethodid uuid,
 ADD COLUMN measurementmethodcode varchar(50),
 ADD COLUMN testmethod text,
 ADD COLUMN inputconfig jsonb,
 ADD COLUMN acceptancerule jsonb,
 ADD COLUMN specificationtext text,
 ADD COLUMN isrequired boolean NOT NULL DEFAULT false,
 ADD COLUMN showonreport boolean NOT NULL DEFAULT true,
 ADD COLUMN showoncoa boolean NOT NULL DEFAULT true,
 ADD COLUMN sortorder integer NOT NULL DEFAULT 0;
ALTER TABLE devandqa."IqcInspectionResult" ADD FOREIGN KEY (measurementmethodid) REFERENCES devandqa."MeasurementMethod"(id) ON DELETE RESTRICT;
UPDATE devandqa."IqcInspectionResult" r SET evaluationstandardid = i.evaluationstandardid,
 hasconfigurationsnapshot = true,
 criterioncode = i.criterioncode,
 criterionname = i.criterionname,
 datatype = i.datatype,
 unit = i.unit,
 measurementmethodid = i.measurementmethodid,
 measurementmethodcode = i.measurementmethodcode,
 testmethod = i.testmethod,
 inputconfig = i.inputconfig,
 acceptancerule = i.acceptancerule,
 specificationtext = i.specificationtext,
 isrequired = i.isrequired,
 showonreport = i.showonreport,
 sortorder = i.sortorder
 FROM devandqa."IqcSpecificationItem" i WHERE r.iqcspecificationitemid = i.id;
ALTER TABLE devandqa."IqcInspectionResult" ADD CONSTRAINT ck_iqcinspectionresult_snapshot
 CHECK (NOT hasconfigurationsnapshot OR (evaluationstandardid IS NOT NULL AND criterioncode <> '' AND criterionname <> '' AND datatype BETWEEN 1 AND 7 AND sortorder >= 0));
CREATE INDEX ix_iqcinspectionresult_sourcetemplateitemid ON devandqa."IqcInspectionResult"(sourcetemplateitemid);
CREATE INDEX ix_iqcinspectionresult_evaluationstandardid ON devandqa."IqcInspectionResult"(evaluationstandardid);
CREATE INDEX ix_iqcinspectionresult_measurementmethodid ON devandqa."IqcInspectionResult"(measurementmethodid);
ALTER TABLE devandqa."CoaCertificateResult"
 ALTER COLUMN coaspecificationitemid DROP NOT NULL,
 ADD COLUMN sourcetemplateitemid uuid REFERENCES devandqa."EvaluationTemplateItem"(id) ON DELETE RESTRICT,
 ADD COLUMN evaluationstandardid uuid REFERENCES devandqa."EvaluationStandard"(id) ON DELETE RESTRICT,
 ADD COLUMN hasconfigurationsnapshot boolean NOT NULL DEFAULT false,
 ADD COLUMN criterioncode citext NOT NULL DEFAULT '',
 ADD COLUMN criterionname varchar(200) NOT NULL DEFAULT '',
 ADD COLUMN datatype integer NOT NULL DEFAULT 1,
 ADD COLUMN unit varchar(50),
 ADD COLUMN measurementmethodid uuid,
 ADD COLUMN measurementmethodcode varchar(50),
 ADD COLUMN testmethod text,
 ADD COLUMN inputconfig jsonb,
 ADD COLUMN acceptancerule jsonb,
 ADD COLUMN specificationtext text,
 ADD COLUMN isrequired boolean NOT NULL DEFAULT false,
 ADD COLUMN showonreport boolean NOT NULL DEFAULT true,
 ADD COLUMN showoncoa boolean NOT NULL DEFAULT true,
 ADD COLUMN sortorder integer NOT NULL DEFAULT 0;
ALTER TABLE devandqa."CoaCertificateResult" ADD FOREIGN KEY (measurementmethodid) REFERENCES devandqa."MeasurementMethod"(id) ON DELETE RESTRICT;
UPDATE devandqa."CoaCertificateResult" r SET evaluationstandardid = i.evaluationstandardid,
 hasconfigurationsnapshot = true,
 criterioncode = i.criterioncode,
 criterionname = i.criterionname,
 datatype = i.datatype,
 unit = i.unit,
 measurementmethodid = i.measurementmethodid,
 measurementmethodcode = i.measurementmethodcode,
 testmethod = i.testmethod,
 inputconfig = i.inputconfig,
 acceptancerule = i.acceptancerule,
 specificationtext = i.specificationtext,
 isrequired = i.isrequired,
 showoncoa = i.showoncoa,
 sortorder = i.sortorder
 FROM devandqa."CoaSpecificationItem" i WHERE r.coaspecificationitemid = i.id;
ALTER TABLE devandqa."CoaCertificateResult" ADD CONSTRAINT ck_coacertificateresult_snapshot
 CHECK (NOT hasconfigurationsnapshot OR (evaluationstandardid IS NOT NULL AND criterioncode <> '' AND criterionname <> '' AND datatype BETWEEN 1 AND 7 AND sortorder >= 0));
CREATE INDEX ix_coacertificateresult_sourcetemplateitemid ON devandqa."CoaCertificateResult"(sourcetemplateitemid);
CREATE INDEX ix_coacertificateresult_evaluationstandardid ON devandqa."CoaCertificateResult"(evaluationstandardid);
CREATE INDEX ix_coacertificateresult_measurementmethodid ON devandqa."CoaCertificateResult"(measurementmethodid);
CREATE INDEX ix_measurementmethod_companyid ON devandqa."MeasurementMethod"(companyid);
CREATE INDEX ix_measurementmethod_createdby ON devandqa."MeasurementMethod"(createdby);
CREATE INDEX ix_measurementmethod_updatedby ON devandqa."MeasurementMethod"(updatedby);
CREATE INDEX ix_evaluationtemplate_companyid ON devandqa."EvaluationTemplate"(companyid);
CREATE INDEX ix_evaluationtemplate_createdby ON devandqa."EvaluationTemplate"(createdby);
CREATE INDEX ix_evaluationtemplate_publishedby ON devandqa."EvaluationTemplate"(publishedby);
CREATE INDEX ix_evaluationstandard_measurementmethodid ON devandqa."EvaluationStandard"(measurementmethodid);
CREATE INDEX ix_iqcspecificationitem_measurementmethodid ON devandqa."IqcSpecificationItem"(measurementmethodid);
CREATE INDEX ix_coaspecificationitem_measurementmethodid ON devandqa."CoaSpecificationItem"(measurementmethodid);
CREATE INDEX ix_evaluationtemplateitem_measurementmethodid ON devandqa."EvaluationTemplateItem"(measurementmethodid);
CREATE INDEX ix_iqcinspectionevaluation_evaluationtemplateid ON devandqa."IqcInspectionEvaluation"(evaluationtemplateid);
CREATE INDEX ix_coacertificate_evaluationtemplateid ON devandqa."CoaCertificate"(evaluationtemplateid);
CREATE INDEX ix_evaluationtemplateitem_evaluationstandardid ON devandqa."EvaluationTemplateItem"(evaluationstandardid);
COMMIT;
