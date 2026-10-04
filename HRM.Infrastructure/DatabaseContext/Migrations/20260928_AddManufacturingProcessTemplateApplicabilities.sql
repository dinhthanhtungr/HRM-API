-- Rules used to suggest released process-template versions from a Formula's product category and production route.
CREATE TABLE IF NOT EXISTS bom.manufacturing_process_template_applicabilities
(
    manufacturing_process_template_applicability_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    manufacturing_process_template_id uuid NOT NULL,
    category_id uuid NULL,
    step_of_product integer NULL,
    priority integer NOT NULL DEFAULT 0,
    note text NULL,
    CONSTRAINT fk_process_template_applicabilities_template
        FOREIGN KEY (manufacturing_process_template_id)
        REFERENCES bom.manufacturing_process_templates(manufacturing_process_template_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_process_template_applicabilities_category
        FOREIGN KEY (category_id)
        REFERENCES "Material"."Categories"("CategoryId")
        ON DELETE RESTRICT,
    CONSTRAINT ck_process_template_applicability_condition_required
        CHECK (category_id IS NOT NULL OR step_of_product IS NOT NULL),
    CONSTRAINT ck_process_template_applicability_priority_non_negative
        CHECK (priority >= 0)
);

CREATE INDEX IF NOT EXISTS ix_process_template_applicabilities_template
    ON bom.manufacturing_process_template_applicabilities(manufacturing_process_template_id);

CREATE INDEX IF NOT EXISTS ix_process_template_applicabilities_match
    ON bom.manufacturing_process_template_applicabilities(category_id, step_of_product);

CREATE UNIQUE INDEX IF NOT EXISTS ux_process_template_applicabilities_rule
    ON bom.manufacturing_process_template_applicabilities
    (
        manufacturing_process_template_id,
        COALESCE(category_id, '00000000-0000-0000-0000-000000000000'::uuid),
        COALESCE(step_of_product, -1)
    );
