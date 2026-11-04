import { describe, expect, it } from "vitest";
import { buildLabWrite, labFormDefaults } from "./labTestForm";
import { labTestSchema } from "./labTest.validation";
import { labTestActions } from "../permissions/labTestPermission";
import type { LabTest } from "../../domain/dtos/LabTest";

describe("LabTest payload and permissions", () => {
  it("preserves omitted metadata and uses explicit clears", () => {
    const baseline = labFormDefaults({
      sampleName: "PP",
      testDate: "2026-01-02",
      note: "Keep",
      itemCode: "Old",
    });
    const next = { ...baseline, itemCode: "" };
    const patch = buildLabWrite(next, baseline);
    expect(patch).toEqual({ clearFields: ["itemCode"], results: [] });
  });
  it("clears observation without losing a legitimate zero", () => {
    const detail = {
      sampleName: "PP",
      testDate: "2026-01-02",
      results: [
        {
          evaluationStandardId: "11111111-1111-4111-8111-111111111111",
          criterionName: "Density",
          value: "0",
          observedText: "Old",
          configuration: { dataType: 2 },
        },
      ],
    } as LabTest;
    const baseline = labFormDefaults(undefined, detail);
    const next = {
      ...baseline,
      results: [{ ...baseline.results[0], observedText: "" }],
    };
    expect(buildLabWrite(next, baseline).results?.[0]).toEqual({
      evaluationStandardId: detail.results[0].evaluationStandardId,
      value: "0",
      clearObservedText: true,
    });
  });
  it("validates missing dates and names before submit", () => {
    expect(
      labTestSchema((key) => key).safeParse(labFormDefaults()).success,
    ).toBe(false);
    expect(
      labTestSchema((key) => key).safeParse(
        labFormDefaults({ sampleName: "PP", testDate: "2026-01-02" }),
      ).success,
    ).toBe(true);
  });
  it("does not expose delete for IQC or users without manage", () => {
    expect(labTestActions(false).canCreate).toBe(false);
    expect(
      labTestActions(true, { source: 1, canDelete: true } as never).canDelete,
    ).toBe(false);
    expect(
      labTestActions(true, { source: 2, canDelete: true } as never).canDelete,
    ).toBe(true);
  });
});
