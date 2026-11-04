"use client";
import { useRef, useState } from "react";
import { useI18n } from "@/core/i18n";
import {
  AppButton,
  AppDialog,
  AppDialogContent,
  AppDialogTitle,
  AppDialogFooter,
  AppNotification,
  PaginationBar,
} from "@/shared/ui";
import { StandardPicker } from "../../../evaluations/presentation/components/EvaluationLookups";
import { useLabImport, useLabImportStandards } from "../hooks/useLabTests";
import { LabTestEditor } from "./LabTestEditor";
import type { LabImportPreview, LabImportRow } from "../../domain/dtos/LabTest";
import styles from "./LabTests.module.css";

export function LabTestImportDialog({
  canImport,
  onClose,
}: {
  canImport: boolean;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const fileInput = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<LabImportPreview | null>(null);
  const [mapping, setMapping] = useState<Record<string, string>>({});
  const [mappingNames, setMappingNames] = useState<Record<string, string>>({});
  const [mappingDirty, setMappingDirty] = useState(false);
  const [selection, setSelection] = useState<Set<number>>(new Set());
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<LabImportRow | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [summary, setSummary] = useState<{
    created: number;
    skipped: number;
  } | null>(null);
  const mutation = useLabImport();
  const standards = useLabImportStandards(
    editing?.data.results?.map((r) => r.evaluationStandardId) ?? [],
    Boolean(editing),
  );
  const pending = busy || mutation.preview.isPending;
  const shift =
    preview?.headers[1]?.replace(/[^a-zA-Z]/g, "").toLowerCase() ===
      "materialcode" || preview?.headers[1]?.toLowerCase().includes("nvl")
      ? 1
      : 0;
  const criterionHeaders = preview?.headers.slice(7 + shift, 20 + shift) ?? [];
  const allMapped = criterionHeaders.every((header) =>
    Boolean(mapping[header]),
  );
  const selectedRows =
    preview?.rows.filter(
      (row) => selection.has(row.rowNumber) && row.errors.length === 0,
    ) ?? [];

  async function readPreview() {
    if (!file || !canImport || pending) return;
    setError(null);
    setSummary(null);
    setConfirmed(false);
    try {
      const result = await mutation.preview.mutateAsync({ file, mapping });
      setPreview(result);
      setMapping(result.columnStandards);
      setMappingDirty(false);
      setPage(1);
      setSelection(
        new Set(
          result.rows
            .filter((row) => row.errors.length === 0)
            .map((row) => row.rowNumber),
        ),
      );
    } catch (issue) {
      setError(
        issue instanceof Error ? issue.message : t("iqc.labTests.failed"),
      );
    }
  }
  async function commit() {
    if (
      !preview ||
      !canImport ||
      !confirmed ||
      pending ||
      mappingDirty ||
      selectedRows.length === 0
    )
      return;
    setBusy(true);
    setError(null);
    let created = 0,
      skipped = 0;
    try {
      for (let offset = 0; offset < selectedRows.length; offset += 500) {
        const rows = selectedRows
          .slice(offset, offset + 500)
          .map(({ rowNumber, data }) => ({ rowNumber, data }));
        const result = await mutation.commit.mutateAsync({
          batchId: preview.importBatchId,
          rows,
        });
        created += result.created;
        skipped += result.skipped;
        // Keep failed/unsubmitted rows selected; successful batches are already stored.
        setSelection(
          (current) =>
            new Set(
              [...current].filter(
                (number) => !rows.some((row) => row.rowNumber === number),
              ),
            ),
        );
        setSummary({ created, skipped });
      }
      setConfirmed(false);
    } catch (issue) {
      setError(
        issue instanceof Error ? issue.message : t("iqc.labTests.failed"),
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <AppDialog
      open
      onOpenChange={(open) => {
        if (!open && !pending) onClose();
      }}
    >
      <AppDialogContent className={styles.importDialog}>
        <AppDialogTitle>{t("iqc.labTests.import")}</AppDialogTitle>
        <div className={styles.editorBody}>
          <p>{t("iqc.labTests.importHint")}</p>
          <input
            ref={fileInput}
            type="file"
            name="iqc.labTests.importFile"
            accept=".csv,.xlsx"
            aria-label={t("iqc.labTests.file")}
            disabled={pending || !canImport}
            onChange={(event) => {
              const selected = event.target.files?.[0] ?? null;
              setFile(selected);
              setPreview(null);
              setMapping({});
              setMappingNames({});
              setMappingDirty(false);
              setError(null);
              setSummary(null);
              if (selected && selected.size > 5_000_000)
                setError(t("iqc.labTests.fileSize"));
            }}
          />
          <AppButton
            type="button"
            disabled={!file || file.size > 5_000_000 || pending || !canImport}
            disabledReason={
              !file
                ? t("iqc.labTests.chooseFile")
                : !canImport
                  ? t("iqc.labTests.manageRequired")
                  : file.size > 5_000_000
                    ? t("iqc.labTests.fileSize")
                    : undefined
            }
            onClick={() => void readPreview()}
          >
            {t(pending ? "iqc.labTests.loading" : "iqc.labTests.preview")}
          </AppButton>
          {preview && (
            <>
              <p>
                {t("iqc.labTests.previewCounts", {
                  count: preview.totalDataRows,
                  ignored: preview.ignoredRows,
                  invalid: preview.rows.filter((row) => row.errors.length)
                    .length,
                })}
              </p>
              {preview.warnings.map((warning, index) => (
                <AppNotification key={index} tone="warning" message={warning} />
              ))}
              <details className={styles.mapping}>
                <summary>{t("iqc.labTests.columnMapping")}</summary>
                <div className={styles.mappingGrid}>
                  {criterionHeaders.map((header) => (
                    <div key={header} className={styles.stack}>
                      <strong>{header}</strong>
                      <span>
                        {mappingNames[header] ??
                          (mapping[header]
                            ? t("iqc.labTests.mapped")
                            : t("iqc.labTests.unmapped"))}
                      </span>
                      <StandardPicker
                        disabled={pending || !canImport}
                        onSelect={(item) => {
                          setMapping((current) => ({
                            ...current,
                            [header]: item.id,
                          }));
                          setMappingNames((current) => ({
                            ...current,
                            [header]: item.name,
                          }));
                          setMappingDirty(true);
                          setConfirmed(false);
                        }}
                      />
                    </div>
                  ))}
                </div>
                <AppButton
                  onClick={() => void readPreview()}
                  disabled={pending || !file}
                >
                  {t("iqc.labTests.repreview")}
                </AppButton>
              </details>
              {mappingDirty && (
                <AppNotification
                  tone="warning"
                  message={t("iqc.labTests.mappingChanged")}
                />
              )}
              <div className={styles.actions}>
                <AppButton
                  variant="outline"
                  disabled={pending}
                  onClick={() => {
                    setSelection(
                      new Set(
                        preview.rows
                          .filter((row) => row.errors.length === 0)
                          .map((row) => row.rowNumber),
                      ),
                    );
                    setConfirmed(false);
                  }}
                >
                  {t("iqc.labTests.selectValid")}
                </AppButton>
                <span>
                  {t("iqc.labTests.selected", { count: selectedRows.length })}
                </span>
              </div>
              <div className={styles.previewViewport}>
                <table className={styles.table}>
                  <thead>
                    <tr>
                      {[
                        "select",
                        "index",
                        "testDate",
                        "materialCode",
                        "itemCode",
                        "sampleName",
                        "results",
                        "issues",
                        "actions",
                      ].map((key) => (
                        <th key={key}>{t(`iqc.labTests.${key}`)}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {preview.rows
                      .slice((page - 1) * 50, page * 50)
                      .map((row) => (
                        <tr
                          key={row.rowNumber}
                          data-invalid={row.errors.length > 0 || undefined}
                        >
                          <td>
                            <input
                              type="checkbox"
                              aria-label={t("iqc.labTests.selectRow", {
                                row: row.rowNumber,
                              })}
                              checked={selection.has(row.rowNumber)}
                              disabled={pending || row.errors.length > 0}
                              onChange={(event) => {
                                setConfirmed(false);
                                setSelection((current) => {
                                  const next = new Set(current);
                                  if (event.target.checked)
                                    next.add(row.rowNumber);
                                  else next.delete(row.rowNumber);
                                  return next;
                                });
                              }}
                            />
                          </td>
                          <td>{row.rowNumber}</td>
                          <td>{row.data.testDate || "—"}</td>
                          <td>{t("iqc.labTests.generatedCode")}</td>
                          <td>{row.data.itemCode || "—"}</td>
                          <td>{row.data.sampleName || "—"}</td>
                          <td>
                            {row.data.results
                              ?.map(
                                (result) =>
                                  result.observedText ?? result.value ?? "—",
                              )
                              .join(" · ")}
                          </td>
                          <td>
                            {[...row.errors, ...row.warnings].join(" · ") ||
                              "—"}
                          </td>
                          <td>
                            <AppButton
                              size="sm"
                              variant="outline"
                              disabled={pending || mappingDirty || !allMapped}
                              disabledReason={
                                mappingDirty || !allMapped
                                  ? t("iqc.labTests.mapFirst")
                                  : undefined
                              }
                              onClick={() => setEditing(row)}
                            >
                              {t("iqc.labTests.edit")}
                            </AppButton>
                          </td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
              <PaginationBar
                pageNumber={page}
                totalPages={Math.max(1, Math.ceil(preview.rows.length / 50))}
                hasPreviousPage={page > 1}
                hasNextPage={page * 50 < preview.rows.length}
                onPageChange={setPage}
              />
              <label className={styles.confirmation}>
                <input
                  type="checkbox"
                  checked={confirmed}
                  disabled={pending || mappingDirty}
                  onChange={(event) => setConfirmed(event.target.checked)}
                />
                {t("iqc.labTests.reviewed")}
              </label>
            </>
          )}
          {error && <AppNotification tone="danger" message={error} />}
          {summary && (
            <AppNotification
              tone="complete"
              message={t("iqc.labTests.imported", summary)}
            />
          )}
        </div>
        <AppDialogFooter>
          <AppButton variant="outline" disabled={pending} onClick={onClose}>
            {t("iqc.labTests.close")}
          </AppButton>
          <AppButton
            disabled={
              !preview ||
              !confirmed ||
              mappingDirty ||
              selectedRows.length === 0 ||
              pending ||
              !canImport
            }
            disabledReason={
              !canImport
                ? t("iqc.labTests.manageRequired")
                : !confirmed
                  ? t("iqc.labTests.reviewRequired")
                  : mappingDirty
                    ? t("iqc.labTests.mappingChanged")
                    : selectedRows.length === 0
                      ? t("iqc.labTests.noSelection")
                      : undefined
            }
            onClick={() => void commit()}
          >
            {t(busy ? "iqc.labTests.importing" : "iqc.labTests.importSelected")}
          </AppButton>
        </AppDialogFooter>
        {editing &&
          (standards.isLoading ? (
            <p role="status">{t("iqc.labTests.loading")}</p>
          ) : standards.isError ? (
            <AppNotification tone="danger" message={standards.error.message} />
          ) : (
            standards.data && (
              <LabTestEditor
                key={editing.rowNumber}
                initial={editing.data}
                standards={standards.data}
                canEdit={canImport}
                pending={false}
                onClose={() => setEditing(null)}
                onSave={async (data) => {
                  setPreview((current) =>
                    current
                      ? {
                          ...current,
                          rows: current.rows.map((row) =>
                            row.rowNumber === editing.rowNumber
                              ? { ...row, data, errors: [] }
                              : row,
                          ),
                        }
                      : null,
                  );
                  setSelection(
                    (current) => new Set([...current, editing.rowNumber]),
                  );
                  setConfirmed(false);
                  setEditing(null);
                }}
              />
            )
          ))}
      </AppDialogContent>
    </AppDialog>
  );
}
