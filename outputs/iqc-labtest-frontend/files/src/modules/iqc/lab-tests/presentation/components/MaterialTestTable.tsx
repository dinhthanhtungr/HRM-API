"use client";
import { useI18n } from "@/core/i18n";
import { AppButton, AppTooltip } from "@/shared/ui";
import type {
  MaterialTestGrid,
  MaterialTestRow,
} from "../../domain/dtos/LabTest";
import { labTestActions } from "../permissions/labTestPermission";
import styles from "./LabTests.module.css";

export function MaterialTestTable({
  data,
  loading,
  canManage,
  onOpen,
  onDelete,
}: {
  data?: MaterialTestGrid;
  loading: boolean;
  canManage: boolean;
  onOpen: (row: MaterialTestRow) => void;
  onDelete: (row: MaterialTestRow) => void;
}) {
  const { t, locale } = useI18n();
  const columns = data?.columns ?? [];
  return (
    <div
      className={styles.tableViewport}
      tabIndex={0}
      role="region"
      aria-label={t("iqc.labTests.title")}
      aria-busy={loading}
    >
      <table className={styles.table}>
        <thead>
          <tr>
            <th className={styles.indexColumn}>{t("iqc.labTests.index")}</th>
            <th className={styles.dateColumn}>{t("iqc.labTests.testDate")}</th>
            <th className={styles.codeColumn}>
              {t("iqc.labTests.materialCode")}
            </th>
            {(
              [
                "source",
                "supplierCustomerName",
                "batchNumber",
                "itemCode",
                "sampleName",
                "resin",
              ] as const
            ).map((key) => (
              <th key={key}>{t(`iqc.labTests.${key}`)}</th>
            ))}
            {columns.map((column) => (
              <th key={column.key}>
                {locale === "en" ? column.nameEn || column.name : column.name}
                {column.unit && <small>{column.unit}</small>}
              </th>
            ))}
            {(
              [
                "application",
                "preparedByName",
                "note",
                "importedPhotoName",
                "weightKg",
              ] as const
            ).map((key) => (
              <th key={key}>{t(`iqc.labTests.${key}`)}</th>
            ))}
            <th>{t("iqc.labTests.actions")}</th>
          </tr>
        </thead>
        <tbody>
          {loading && !data && (
            <tr>
              <td colSpan={15 + columns.length} role="status">
                {t("iqc.labTests.loading")}
              </td>
            </tr>
          )}
          {!loading && !data?.page.items.length && (
            <tr>
              <td colSpan={15 + columns.length}>{t("iqc.labTests.empty")}</td>
            </tr>
          )}
          {data?.page.items.map((row, index) => {
            const actions = labTestActions(canManage, row);
            const cells = new Map(row.cells.map((cell) => [cell.key, cell]));
            return (
              <tr key={`${row.source}:${row.id}`}>
                <td className={styles.indexColumn}>
                  {(data.page.pageNumber - 1) * data.page.pageSize + index + 1}
                </td>
                <td className={styles.dateColumn}>{row.testDate}</td>
                <td className={styles.codeColumn}>
                  <button
                    type="button"
                    className={styles.codeLink}
                    onClick={() => onOpen(row)}
                  >
                    {row.materialCode || "—"}
                  </button>
                </td>
                <td>
                  {t(
                    row.source === 1
                      ? "iqc.labTests.sourceIqc"
                      : "iqc.labTests.sourceLab",
                  )}
                </td>
                {(
                  [
                    "supplierCustomerName",
                    "batchNumber",
                    "itemCode",
                    "sampleName",
                    "resin",
                  ] as const
                ).map((key) => (
                  <td key={key}>{row[key] ?? "—"}</td>
                ))}
                {columns.map((column) => {
                  const cell = cells.get(column.key);
                  return (
                    <td key={column.key}>
                      {cell ? (
                        <AppTooltip
                          content={[
                            cell.configuration.unit,
                            cell.configuration.testMethod,
                            cell.configuration.specificationText,
                          ]
                            .filter(Boolean)
                            .join(" · ")}
                        >
                          <span className={styles.observation}>
                            {cell.observedText ?? cell.value ?? "—"}
                          </span>
                        </AppTooltip>
                      ) : (
                        "—"
                      )}
                    </td>
                  );
                })}
                {(
                  [
                    "application",
                    "preparedByName",
                    "note",
                    "importedPhotoName",
                    "weightKg",
                  ] as const
                ).map((key) => (
                  <td key={key}>{row[key] ?? "—"}</td>
                ))}
                <td>
                  <div className={styles.actions}>
                    <AppButton
                      size="sm"
                      variant="outline"
                      onClick={() => onOpen(row)}
                    >
                      {t(
                        actions.canEdit
                          ? "iqc.labTests.edit"
                          : "iqc.labTests.view",
                      )}
                    </AppButton>
                    {row.source === 2 && (
                      <AppButton
                        size="sm"
                        variant="outline"
                        disabled={!actions.canDelete}
                        disabledReason={t(actions.disabledReasonKey)}
                        onClick={() => onDelete(row)}
                      >
                        {t("iqc.labTests.delete")}
                      </AppButton>
                    )}
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      {loading && data && (
        <div className={styles.loadingOverlay} role="status">
          {t("iqc.labTests.loading")}
        </div>
      )}
    </div>
  );
}
