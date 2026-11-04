import type {
  LabConfiguration,
  LabTest,
  LabWrite,
} from "../../domain/dtos/LabTest";
import type { EvaluationStandard } from "../../../evaluation-standards/domain/dtos/EvaluationStandard";
import { resultValueConfiguration } from "../../../evaluations/presentation/form/resultValueConfiguration";

export const metadataFields = [
  ["sampleName", 200],
  ["supplierCustomerName", 300],
  ["batchNumber", 100],
  ["itemCode", 100],
  ["resin", 100],
  ["application", 2000],
  ["preparedByName", 200],
  ["importedPhotoName", 255],
  ["note", 16000],
] as const;
export type LabFormResult = {
  standardId: string;
  name: string;
  configuration: LabConfiguration;
  value: string;
  observedText: string;
};
export type LabFormValues = Record<
  (typeof metadataFields)[number][0],
  string
> & {
  testDate: string;
  weightKg: string;
  results: LabFormResult[];
};
export function standardConfiguration(
  item: EvaluationStandard,
): LabConfiguration {
  return {
    dataType: item.dataType,
    unit: item.unit,
    measurementMethodId: item.measurementMethodId,
    measurementMethodCode: null,
    testMethod: item.testMethod,
    inputConfigJson: item.inputConfigJson,
    acceptanceRuleJson: item.acceptanceRuleJson,
    specificationText: item.specificationText,
    isRequired: item.isRequired,
    sortOrder: item.sortOrder,
  };
}
export function scalarConfiguration(config: LabConfiguration) {
  return resultValueConfiguration({
    ...config,
    showOnReport: false,
    showOnCoa: false,
  });
}
export function labFormDefaults(
  data?: LabWrite,
  detail?: LabTest,
  standards: EvaluationStandard[] = [],
): LabFormValues {
  const source = detail ?? data;
  const fields = Object.fromEntries(
    metadataFields.map(([name]) => [name, source?.[name] ?? ""]),
  ) as Pick<LabFormValues, (typeof metadataFields)[number][0]>;
  const results: LabFormResult[] = detail
    ? detail.results
        .filter((r) => Boolean(r.evaluationStandardId))
        .map((r) => ({
          standardId: r.evaluationStandardId!,
          name: r.criterionName,
          configuration: r.configuration,
          value: r.value ?? "",
          observedText: r.observedText ?? "",
        }))
    : (data?.results ?? []).flatMap((r) => {
        const standard = standards.find((s) => s.id === r.evaluationStandardId);
        return standard
          ? [
              {
                standardId: standard.id,
                name: standard.name,
                configuration: standardConfiguration(standard),
                value: r.value ?? "",
                observedText: r.observedText ?? "",
              },
            ]
          : [];
      });
  return {
    ...fields,
    testDate: source?.testDate?.slice(0, 10) ?? "",
    weightKg: source?.weightKg == null ? "" : String(source.weightKg),
    results,
  };
}
export function buildLabWrite(
  values: LabFormValues,
  baseline?: LabFormValues,
): LabWrite {
  const data: LabWrite = {};
  const clearFields: string[] = [];
  for (const [field] of metadataFields) {
    if (baseline && values[field] === baseline[field]) continue;
    const value = values[field].trim();
    if (field === "sampleName") data.sampleName = value;
    else if (value || !baseline) data[field] = value || null;
    else clearFields.push(field);
  }
  if (!baseline || values.testDate !== baseline.testDate)
    data.testDate = values.testDate.slice(0, 10);
  if (!baseline || values.weightKg !== baseline.weightKg) {
    if (!values.weightKg.trim() && baseline) clearFields.push("weightKg");
    else
      data.weightKg = values.weightKg.trim()
        ? Number(values.weightKg.replace(",", "."))
        : null;
  }
  if (clearFields.length) data.clearFields = clearFields;
  data.results = values.results.flatMap((r) => {
    const old = baseline?.results.find((x) => x.standardId === r.standardId);
    if (old && r.value === old.value && r.observedText === old.observedText)
      return [];
    return [
      {
        evaluationStandardId: r.standardId,
        ...(r.value.trim()
          ? { value: r.value.trim() }
          : old
            ? { clearValue: true }
            : {}),
        ...(r.observedText.trim()
          ? { observedText: r.observedText.trim() }
          : old
            ? { clearObservedText: true }
            : {}),
      },
    ];
  });
  return data;
}
