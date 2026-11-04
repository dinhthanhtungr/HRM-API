"use client";
import { useMemo, useState } from "react";
import {
  FormProvider,
  Controller,
  useFieldArray,
  useForm,
} from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useI18n } from "@/core/i18n";
import {
  AppButton,
  AppDialog,
  AppDialogContent,
  AppDialogTitle,
  AppDialogFooter,
  AppNotification,
  AppDatePicker,
  RHFAppInput,
  FormGrid,
  ConfiguredValueInput,
} from "@/shared/ui";
import { StandardPicker } from "../../../evaluations/presentation/components/EvaluationLookups";
import type { EvaluationStandard } from "../../../evaluation-standards/domain/dtos/EvaluationStandard";
import type { LabTest, LabWrite } from "../../domain/dtos/LabTest";
import {
  buildLabWrite,
  labFormDefaults,
  metadataFields,
  scalarConfiguration,
  standardConfiguration,
  type LabFormValues,
} from "../form/labTestForm";
import { labTestSchema } from "../form/labTest.validation";
import styles from "./LabTests.module.css";

export function LabTestEditor({
  detail,
  initial,
  standards,
  canEdit,
  pending,
  error,
  onSave,
  onClose,
}: {
  detail?: LabTest;
  initial?: LabWrite;
  standards?: EvaluationStandard[];
  canEdit: boolean;
  pending: boolean;
  error?: string;
  onSave: (data: LabWrite) => Promise<void>;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const [discard, setDiscard] = useState(false);
  const baseline = useMemo(
    () => labFormDefaults(initial, detail, standards),
    [initial, detail, standards],
  );
  const schema = useMemo(() => labTestSchema(t), [t]);
  const form = useForm<LabFormValues>({
    defaultValues: baseline,
    resolver: zodResolver(schema),
  });
  const results = useFieldArray({ control: form.control, name: "results" });
  const blocked = pending || !canEdit;
  const close = () => {
    if (pending) return;
    if (form.formState.isDirty) setDiscard(true);
    else onClose();
  };
  return (
    <AppDialog
      open
      onOpenChange={(open) => {
        if (!open) close();
      }}
    >
      <AppDialogContent className={styles.editorDialog}>
        <AppDialogTitle>
          {detail?.code ?? t("iqc.labTests.create")}
        </AppDialogTitle>
        <FormProvider {...form}>
          <form
            className={styles.stack}
            onSubmit={form.handleSubmit(async (values) => {
              if (blocked) return;
              try {
                await onSave(
                  buildLabWrite(values, detail ? baseline : undefined),
                );
              } catch {
                /* Keep draft; mutation error is displayed below. */
              }
            })}
          >
            <div className={styles.editorBody}>
              <FormGrid columns={2}>
                {metadataFields
                  .filter(([name]) => name !== "note")
                  .map(([name, maxLength]) => (
                    <RHFAppInput
                      key={name}
                      name={name}
                      inputName={`iqc.labTests.${name}`}
                      label={t(`iqc.labTests.${name}`)}
                      maxLength={maxLength}
                      autoComplete="off"
                      disabled={blocked}
                    />
                  ))}
                <Controller
                  name="testDate"
                  control={form.control}
                  render={({ field, fieldState }) => (
                    <AppDatePicker
                      name="iqc.labTests.testDate"
                      label={t("iqc.labTests.testDate")}
                      value={field.value || null}
                      onChange={(value) =>
                        field.onChange(value?.slice(0, 10) ?? "")
                      }
                      error={fieldState.error?.message}
                      disabled={blocked}
                    />
                  )}
                />
                <RHFAppInput
                  name="weightKg"
                  inputName="iqc.labTests.weightKg"
                  label={t("iqc.labTests.weightKg")}
                  inputMode="decimal"
                  autoComplete="off"
                  disabled={blocked}
                />
              </FormGrid>
              <RHFAppInput
                name="note"
                inputName="iqc.labTests.note"
                label={t("iqc.labTests.note")}
                maxLength={16000}
                autoComplete="off"
                disabled={blocked}
              />
              <StandardPicker
                disabled={blocked || results.fields.length >= 200}
                excluded={results.fields.map((r) => r.standardId)}
                onSelect={(item) => {
                  if (
                    blocked ||
                    results.fields.length >= 200 ||
                    results.fields.some((r) => r.standardId === item.id)
                  )
                    return;
                  results.append({
                    standardId: item.id,
                    name: item.name,
                    configuration: standardConfiguration(item),
                    value: "",
                    observedText: "",
                  });
                }}
              />
              {results.fields.map((result, index) => (
                <div key={result.id} className={styles.resultEditor}>
                  {result.configuration.dataType === 8 ? (
                    <AppNotification
                      tone="warning"
                      message={t("iqc.labTests.attachmentPending")}
                    />
                  ) : (
                    <>
                      <Controller
                        name={`results.${index}.value`}
                        control={form.control}
                        render={({ field, fieldState }) => (
                          <ConfiguredValueInput
                            configuration={scalarConfiguration(
                              result.configuration,
                            )}
                            inputName={`iqc.labTests.result.${result.standardId}`}
                            label={`${result.name}${result.configuration.unit ? ` (${result.configuration.unit})` : ""}`}
                            value={field.value}
                            onChange={field.onChange}
                            error={fieldState.error?.message}
                            disabled={blocked}
                          />
                        )}
                      />
                      <RHFAppInput
                        name={`results.${index}.observedText`}
                        inputName={`iqc.labTests.observation.${result.standardId}`}
                        label={t("iqc.labTests.observedText")}
                        maxLength={16000}
                        autoComplete="off"
                        disabled={blocked}
                      />
                    </>
                  )}
                </div>
              ))}
              {error && <AppNotification tone="danger" message={error} />}
              {discard && (
                <div className={styles.stack}>
                  <AppNotification
                    tone="warning"
                    message={t("iqc.labTests.discardConfirm")}
                  />
                  <div className={styles.actions}>
                    <AppButton
                      type="button"
                      variant="destructive"
                      onClick={() => {
                        form.reset(baseline);
                        onClose();
                      }}
                    >
                      {t("iqc.labTests.discard")}
                    </AppButton>
                    <AppButton
                      type="button"
                      variant="outline"
                      onClick={() => setDiscard(false)}
                    >
                      {t("iqc.labTests.continueEditing")}
                    </AppButton>
                  </div>
                </div>
              )}
            </div>
            <AppDialogFooter>
              <AppButton
                type="button"
                variant="outline"
                onClick={close}
                disabled={pending}
              >
                {t("iqc.labTests.cancel")}
              </AppButton>
              <AppButton
                type="submit"
                disabled={blocked}
                disabledReason={
                  !canEdit ? t("iqc.labTests.manageRequired") : undefined
                }
              >
                {t(pending ? "iqc.labTests.saving" : "iqc.labTests.save")}
              </AppButton>
            </AppDialogFooter>
          </form>
        </FormProvider>
      </AppDialogContent>
    </AppDialog>
  );
}
