-- E-BOM/M-BOM master tables. APIs currently expose Engineering BOM only.
CREATE SCHEMA IF NOT EXISTS bom;

CREATE TABLE IF NOT EXISTS bom.bom_definitions (
  bom_definition_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), company_id uuid NOT NULL, product_id uuid NOT NULL,
  code citext NOT NULL, name varchar(200) NOT NULL, bom_type citext NOT NULL, description text NULL,
  is_active boolean NOT NULL DEFAULT true, created_date timestamp without time zone NOT NULL, created_by uuid NOT NULL,
  updated_date timestamp without time zone NULL, updated_by uuid NULL,
  CONSTRAINT ck_bom_definitions_type CHECK (bom_type IN ('Engineering', 'Manufacturing')),
  CONSTRAINT fk_bom_definitions_company FOREIGN KEY (company_id) REFERENCES company."Companies"("CompanyId") ON DELETE RESTRICT,
  CONSTRAINT fk_bom_definitions_product FOREIGN KEY (product_id) REFERENCES "SampleRequests"."Products"("ProductId") ON DELETE RESTRICT,
  CONSTRAINT fk_bom_definitions_created_by FOREIGN KEY (created_by) REFERENCES hr."Employees"("EmployeeId") ON DELETE RESTRICT,
  CONSTRAINT fk_bom_definitions_updated_by FOREIGN KEY (updated_by) REFERENCES hr."Employees"("EmployeeId") ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS ux_bom_definitions_company_code ON bom.bom_definitions(company_id, code);
CREATE INDEX IF NOT EXISTS ix_bom_definitions_company_product_type_active ON bom.bom_definitions(company_id, product_id, bom_type, is_active);

CREATE TABLE IF NOT EXISTS bom.bom_versions (
  bom_version_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), bom_definition_id uuid NOT NULL, version_no integer NOT NULL,
  status citext NOT NULL DEFAULT 'Draft', base_output_quantity numeric(18,3) NOT NULL, output_unit citext NOT NULL,
  source_engineering_bom_version_id uuid NULL, effective_from timestamp without time zone NULL, effective_to timestamp without time zone NULL,
  change_reason text NULL, note text NULL, created_date timestamp without time zone NOT NULL, created_by uuid NOT NULL,
  released_date timestamp without time zone NULL, released_by uuid NULL,
  CONSTRAINT ck_bom_versions_version_no_positive CHECK (version_no > 0), CONSTRAINT ck_bom_versions_base_output_positive CHECK (base_output_quantity > 0),
  CONSTRAINT ck_bom_versions_status CHECK (status IN ('Draft', 'Released', 'Obsolete')),
  CONSTRAINT ck_bom_versions_effective_period CHECK (effective_to IS NULL OR effective_from IS NULL OR effective_to > effective_from),
  CONSTRAINT fk_bom_versions_definition FOREIGN KEY (bom_definition_id) REFERENCES bom.bom_definitions(bom_definition_id) ON DELETE RESTRICT,
  CONSTRAINT fk_bom_versions_source_engineering FOREIGN KEY (source_engineering_bom_version_id) REFERENCES bom.bom_versions(bom_version_id) ON DELETE RESTRICT,
  CONSTRAINT fk_bom_versions_created_by FOREIGN KEY (created_by) REFERENCES hr."Employees"("EmployeeId") ON DELETE RESTRICT,
  CONSTRAINT fk_bom_versions_released_by FOREIGN KEY (released_by) REFERENCES hr."Employees"("EmployeeId") ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS ux_bom_versions_definition_version_no ON bom.bom_versions(bom_definition_id, version_no);
CREATE INDEX IF NOT EXISTS ix_bom_versions_definition_status ON bom.bom_versions(bom_definition_id, status);

CREATE TABLE IF NOT EXISTS bom.manufacturing_bom_stages (
  manufacturing_bom_stage_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), bom_version_id uuid NOT NULL, code citext NOT NULL, name varchar(200) NOT NULL,
  sequence_no integer NOT NULL, description text NULL, is_active boolean NOT NULL DEFAULT true,
  CONSTRAINT ck_manufacturing_bom_stages_sequence_positive CHECK (sequence_no > 0),
  CONSTRAINT fk_manufacturing_bom_stages_version FOREIGN KEY (bom_version_id) REFERENCES bom.bom_versions(bom_version_id) ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS ux_manufacturing_bom_stages_version_code ON bom.manufacturing_bom_stages(bom_version_id, code);
CREATE UNIQUE INDEX IF NOT EXISTS ux_manufacturing_bom_stages_version_sequence ON bom.manufacturing_bom_stages(bom_version_id, sequence_no);

CREATE TABLE IF NOT EXISTS bom.bom_version_items (
  bom_version_item_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), bom_version_id uuid NOT NULL, line_no integer NOT NULL, item_type citext NOT NULL,
  material_id uuid NULL, component_product_id uuid NULL, category_id uuid NULL, manufacturing_bom_stage_id uuid NULL,
  quantity numeric(18,3) NOT NULL, unit citext NOT NULL, material_external_id_snapshot citext NULL, material_name_snapshot varchar(300) NULL, note text NULL,
  CONSTRAINT ck_bom_version_items_line_no_positive CHECK (line_no > 0), CONSTRAINT ck_bom_version_items_quantity_positive CHECK (quantity > 0),
  CONSTRAINT ck_bom_version_items_component CHECK ((item_type = 'Material' AND material_id IS NOT NULL AND component_product_id IS NULL) OR (item_type = 'Product' AND component_product_id IS NOT NULL AND material_id IS NULL)),
  CONSTRAINT fk_bom_version_items_version FOREIGN KEY (bom_version_id) REFERENCES bom.bom_versions(bom_version_id) ON DELETE RESTRICT,
  CONSTRAINT fk_bom_version_items_material FOREIGN KEY (material_id) REFERENCES "Material"."Materials"("MaterialId") ON DELETE RESTRICT,
  CONSTRAINT fk_bom_version_items_component_product FOREIGN KEY (component_product_id) REFERENCES "SampleRequests"."Products"("ProductId") ON DELETE RESTRICT,
  CONSTRAINT fk_bom_version_items_category FOREIGN KEY (category_id) REFERENCES "Material"."Categories"("CategoryId") ON DELETE RESTRICT,
  CONSTRAINT fk_bom_version_items_stage FOREIGN KEY (manufacturing_bom_stage_id) REFERENCES bom.manufacturing_bom_stages(manufacturing_bom_stage_id) ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS ux_bom_version_items_version_line_no ON bom.bom_version_items(bom_version_id, line_no);

CREATE TABLE IF NOT EXISTS bom.manufacturing_loss_types (
  manufacturing_loss_type_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), company_id uuid NOT NULL, code citext NOT NULL, name varchar(200) NOT NULL,
  description text NULL, default_calculation_method citext NOT NULL, is_recoverable boolean NOT NULL DEFAULT false, is_active boolean NOT NULL DEFAULT true,
  created_date timestamp without time zone NOT NULL, created_by uuid NOT NULL, updated_date timestamp without time zone NULL, updated_by uuid NULL,
  CONSTRAINT fk_manufacturing_loss_types_company FOREIGN KEY (company_id) REFERENCES company."Companies"("CompanyId") ON DELETE RESTRICT,
  CONSTRAINT fk_manufacturing_loss_types_created_by FOREIGN KEY (created_by) REFERENCES hr."Employees"("EmployeeId") ON DELETE RESTRICT,
  CONSTRAINT fk_manufacturing_loss_types_updated_by FOREIGN KEY (updated_by) REFERENCES hr."Employees"("EmployeeId") ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS ux_manufacturing_loss_types_company_code ON bom.manufacturing_loss_types(company_id, code);

CREATE TABLE IF NOT EXISTS bom.manufacturing_bom_loss_rules (
  manufacturing_bom_loss_rule_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), bom_version_id uuid NOT NULL, manufacturing_loss_type_id uuid NOT NULL,
  bom_version_item_id uuid NULL, manufacturing_bom_stage_id uuid NULL, calculation_method citext NOT NULL, rate_percent numeric(9,6) NULL,
  fixed_quantity_kg numeric(18,3) NULL, quantity_per_event_kg numeric(18,3) NULL, default_event_count integer NULL, sequence_no integer NOT NULL,
  is_recoverable boolean NOT NULL DEFAULT false, include_in_material_request boolean NOT NULL DEFAULT false, is_active boolean NOT NULL DEFAULT true, note text NULL,
  CONSTRAINT fk_manufacturing_bom_loss_rules_version FOREIGN KEY (bom_version_id) REFERENCES bom.bom_versions(bom_version_id) ON DELETE RESTRICT,
  CONSTRAINT fk_manufacturing_bom_loss_rules_type FOREIGN KEY (manufacturing_loss_type_id) REFERENCES bom.manufacturing_loss_types(manufacturing_loss_type_id) ON DELETE RESTRICT,
  CONSTRAINT fk_manufacturing_bom_loss_rules_item FOREIGN KEY (bom_version_item_id) REFERENCES bom.bom_version_items(bom_version_item_id) ON DELETE RESTRICT,
  CONSTRAINT fk_manufacturing_bom_loss_rules_stage FOREIGN KEY (manufacturing_bom_stage_id) REFERENCES bom.manufacturing_bom_stages(manufacturing_bom_stage_id) ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS ux_manufacturing_bom_loss_rules_version_sequence ON bom.manufacturing_bom_loss_rules(bom_version_id, sequence_no);

CREATE TABLE IF NOT EXISTS bom.product_standard_bom_versions (
  product_standard_bom_version_id uuid PRIMARY KEY DEFAULT gen_random_uuid(), company_id uuid NOT NULL, product_id uuid NOT NULL, bom_version_id uuid NOT NULL,
  valid_from timestamp without time zone NOT NULL, valid_to timestamp without time zone NULL, note text NULL, created_date timestamp without time zone NOT NULL,
  created_by uuid NOT NULL, closed_date timestamp without time zone NULL, closed_by uuid NULL,
  CONSTRAINT ck_product_standard_bom_versions_period CHECK (valid_to IS NULL OR valid_to > valid_from),
  CONSTRAINT fk_product_standard_bom_versions_company FOREIGN KEY (company_id) REFERENCES company."Companies"("CompanyId") ON DELETE RESTRICT,
  CONSTRAINT fk_product_standard_bom_versions_product FOREIGN KEY (product_id) REFERENCES "SampleRequests"."Products"("ProductId") ON DELETE RESTRICT,
  CONSTRAINT fk_product_standard_bom_versions_version FOREIGN KEY (bom_version_id) REFERENCES bom.bom_versions(bom_version_id) ON DELETE RESTRICT,
  CONSTRAINT fk_product_standard_bom_versions_created_by FOREIGN KEY (created_by) REFERENCES hr."Employees"("EmployeeId") ON DELETE RESTRICT,
  CONSTRAINT fk_product_standard_bom_versions_closed_by FOREIGN KEY (closed_by) REFERENCES hr."Employees"("EmployeeId") ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS ux_product_standard_bom_versions_current ON bom.product_standard_bom_versions(company_id, product_id) WHERE valid_to IS NULL;
