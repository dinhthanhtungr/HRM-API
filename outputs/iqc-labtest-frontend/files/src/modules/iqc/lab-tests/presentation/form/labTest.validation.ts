import { z } from "zod";
import { metadataFields, scalarConfiguration } from "./labTestForm";
import { validateConfiguredValue } from "@/shared/ui/Forms/Inputs/ConfiguredValueInput/valueRules";
import type { LabConfiguration } from "../../domain/dtos/LabTest";

export function labTestSchema(t: (key: string) => string) {
  const shape = Object.fromEntries(
    metadataFields.map(([name, length]) => [
      name,
      z.string().max(length, t("iqc.labTests.tooLong")),
    ]),
  ) as Record<(typeof metadataFields)[number][0], z.ZodString>;
  return z
    .object({
      ...shape,
      sampleName: z
        .string()
        .trim()
        .min(1, t("iqc.labTests.required"))
        .max(200, t("iqc.labTests.tooLong")),
      testDate: z
        .string()
        .refine(
          (v) =>
            /^\d{4}-\d{2}-\d{2}$/.test(v) &&
            v !== "0001-01-01" &&
            validateConfiguredValue({ kind: "date" }, v) === null,
          t("iqc.labTests.invalidDate"),
        ),
      weightKg: z
        .string()
        .refine(
          (v) => !v.trim() || /^\d{1,12}([.,]\d{1,6})?$/.test(v),
          t("iqc.labTests.invalidWeight"),
        ),
      results: z
        .array(
          z.object({
            standardId: z.string().uuid(),
            name: z.string(),
            configuration: z.custom<LabConfiguration>(),
            value: z.string(),
            observedText: z.string().max(16000, t("iqc.labTests.tooLong")),
          }),
        )
        .max(200, t("iqc.labTests.tooManyCriteria")),
    })
    .superRefine((values, context) => {
      values.results.forEach((r, index) => {
        if (r.configuration.dataType === 8 && (r.value || r.observedText))
          context.addIssue({
            code: "custom",
            path: ["results", index, "value"],
            message: t("iqc.labTests.attachmentPending"),
          });
        const issue =
          r.configuration.dataType === 8
            ? null
            : validateConfiguredValue(
                scalarConfiguration(r.configuration),
                r.value,
              );
        if (issue)
          context.addIssue({
            code: "custom",
            path: ["results", index, "value"],
            message: t(issue),
          });
      });
    });
}
